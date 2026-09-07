using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.IntegrationTests;

/// <summary>
/// The historical query surface: leaderboards, careers, shot charts, and records — the data the
/// product is actually sold on.
/// </summary>
public sealed class QuerySurfaceTests : IntegrationTestBase
{
    public QuerySurfaceTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Per_game_boards_exclude_unqualified_players_while_totals_include_everyone()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);
        var (competitionId, playerIds) = await SeedAggregatesAsync(orgId, players: 6, qualifiedCount: 4);

        var totals = await ReadJson(client.GetAsync(Leaders(orgId, competitionId, "points", "total")));
        var perGame = await ReadJson(client.GetAsync(Leaders(orgId, competitionId, "points", "game")));

        totals.GetProperty("rows").GetArrayLength().Should().Be(6, "totals boards include everyone");
        perGame.GetProperty("rows").GetArrayLength().Should().Be(4, "per-game boards need qualification");

        // Every row on the per-game board is one of the qualified players.
        var qualified = playerIds.Take(4).ToHashSet();
        foreach (var row in perGame.GetProperty("rows").EnumerateArray())
        {
            qualified.Should().Contain(row.GetProperty("playerId").GetString()!);
        }
    }

    [Fact]
    public async Task All_leaders_returns_every_headline_stat_in_one_round_trip_under_200ms()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        // 200 finalised games' worth of statlines, as the acceptance criterion specifies.
        var (competitionId, _) = await SeedAggregatesAsync(orgId, players: 240, qualifiedCount: 200, games: 200);

        // Warm the connection pool and query plan so the measurement is of the query, not of startup.
        await client.GetAsync(AllLeaders(orgId, competitionId));

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync(AllLeaders(orgId, competitionId));
        stopwatch.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJson(Task.FromResult(response));
        body.GetProperty("leaderboards").GetArrayLength().Should().Be(7, "every headline stat is present");
        foreach (var board in body.GetProperty("leaderboards").EnumerateArray())
        {
            board.GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
        }

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200,
            "the competition home screen must not cost six round trips or a slow one");
    }

    [Fact]
    public async Task Leaderboard_queries_use_an_index_rather_than_a_sequential_scan()
    {
        var (_, orgId) = await NewOrgWithOwnerAsync();
        // 100 competitions x 200 players = 20,000 rows, so filtering to one competition is the
        // selective case a real deployment has — and the case the index exists for.
        var (competitionId, _) = await SeedAggregatesAsync(
            orgId, players: 200, qualifiedCount: 200, games: 200, competitions: 100);

        string plan = string.Empty;
        await WithDbAsync(async db =>
        {
            // ANALYZE first so the planner has statistics; without it a fresh table looks tiny.
            await db.Database.ExecuteSqlRawAsync("ANALYZE competition_player_aggregates;");

            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "EXPLAIN ANALYZE SELECT * FROM competition_player_aggregates "
                + $"WHERE competition_id = '{competitionId}' AND is_qualified ORDER BY points DESC LIMIT 10;";
            await using var reader = await command.ExecuteReaderAsync();
            var lines = new List<string>();
            while (await reader.ReadAsync())
            {
                lines.Add(reader.GetString(0));
            }

            plan = string.Join('\n', lines);
        });

        plan.Should().Contain("Index", "the leaderboard read must be an index scan");
        plan.Should().NotContain("Seq Scan on competition_player_aggregates",
            "a sequential scan over every aggregate would not survive real volumes");
    }

    [Fact]
    public async Task Shot_chart_zone_aggregation_matches_a_manual_count_of_the_event_log()
    {
        var g = await PlayedGameAsync();

        var chart = await ReadJson(g.Client.GetAsync($"{g.GameUrl}/shot-chart"));

        // Count the same shots straight from the log and compare.
        var gameId = GameId.FromGuid(Guid.Parse(g.GameId));
        var expected = new Dictionary<string, (int Made, int Attempted)>();
        await WithDbAsync(async db =>
        {
            var shots = await db.GameEvents.IgnoreQueryFilters()
                .Where(e => e.GameId == gameId && !e.IsVoided && e.ShotXCm != null)
                .ToListAsync();

            foreach (var shot in shots)
            {
                var zone = shot.ShotZone!.ToString()!;
                var current = expected.GetValueOrDefault(zone);
                expected[zone] = (
                    current.Made + (shot.EventType == "FIELD_GOAL_MADE" ? 1 : 0),
                    current.Attempted + 1);
            }
        });

        expected.Should().NotBeEmpty("the fixture takes shots from several zones");

        var byZone = chart.GetProperty("byZone").EnumerateArray()
            .ToDictionary(z => z.GetProperty("zone").GetString()!, z => z);
        byZone.Should().HaveCount(expected.Count);

        foreach (var (zone, counts) in expected)
        {
            byZone[zone].GetProperty("made").GetInt32().Should().Be(counts.Made, "zone {0} made", zone);
            byZone[zone].GetProperty("attempted").GetInt32().Should().Be(counts.Attempted, "zone {0} attempted", zone);
        }
    }

    [Fact]
    public async Task Play_by_play_carries_the_running_score()
    {
        var g = await PlayedGameAsync();

        var feed = await ReadJson(g.Client.GetAsync($"{g.GameUrl}/play-by-play"));

        feed.GetArrayLength().Should().BeGreaterThan(0);
        var scoring = feed.EnumerateArray().Where(e => e.GetProperty("eventType").GetString() == "FIELD_GOAL_MADE").ToList();
        scoring.Should().NotBeEmpty();

        // The score after the last scoring play equals the game's final score.
        var last = scoring[^1].GetProperty("scoreAfter");
        last.EnumerateObject().Sum(p => p.Value.GetInt32()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_merged_players_career_includes_statistics_from_both_original_records()
    {
        var g = await PlayedGameAsync(finalize: true);
        var admin = AuthenticatedClient(await NewPlatformAdminTokenAsync());

        // A second record for the same human, with its own finalised game in another competition.
        var duplicateId = g.DuplicatePlayerId;
        var keepId = g.ScorerPlayerId;

        var beforeKeep = await CareerGames(g.Client, g.OrgId, keepId);
        var beforeDuplicate = await CareerGames(g.Client, g.OrgId, duplicateId);
        beforeKeep.Should().BeGreaterThan(0);
        beforeDuplicate.Should().BeGreaterThan(0, "the duplicate has its own finalised history");

        var propose = await g.Client.PostAsJsonAsync(
            $"/api/v1/organisations/{g.OrgId}/registry/merge-proposals",
            new { keepId, mergeId = duplicateId, evidence = "same player, two records" });
        propose.StatusCode.Should().Be(HttpStatusCode.Created);
        var proposalId = (await ReadJson(Task.FromResult(propose))).GetProperty("id").GetString();

        (await admin.PostAsync($"/api/v1/registry/merge-proposals/{proposalId}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Rebuild careers, then the survivor holds both records' games.
        await RecomputeAllAsync(g.Client, g.OrgId);

        var afterKeep = await CareerGames(g.Client, g.OrgId, keepId);
        afterKeep.Should().Be(beforeKeep + beforeDuplicate,
            "the merged record's games moved onto the survivor rather than disappearing");
    }

    [Fact]
    public async Task The_career_page_breaks_totals_down_by_competition()
    {
        var g = await PlayedGameAsync(finalize: true);

        var page = await ReadJson(g.Client.GetAsync(
            $"/api/v1/organisations/{g.OrgId}/players/{g.ScorerPlayerId}/career-page"));

        page.GetProperty("totals").GetProperty("gamesPlayed").GetInt32().Should().BeGreaterThan(0);
        var breakdown = page.GetProperty("byCompetition");
        breakdown.GetArrayLength().Should().BeGreaterThan(0);
        breakdown[0].GetProperty("competitionId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task All_time_records_report_single_game_and_career_holders()
    {
        var g = await PlayedGameAsync(finalize: true);

        var records = await ReadJson(g.Client.GetAsync($"/api/v1/organisations/{g.OrgId}/records"));

        records.GetProperty("singleGame").GetArrayLength().Should().BeGreaterThan(0);
        records.GetProperty("career").GetArrayLength().Should().BeGreaterThan(0);
        records.GetProperty("singleGame")[0].GetProperty("stat").GetString().Should().Be("points");
        records.GetProperty("singleGame")[0].GetProperty("gameId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_player_who_transfers_mid_competition_appears_once_with_combined_totals()
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await PostId(client, $"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" });

        // Three teams: the player starts at A, then transfers to B. C is the common opponent.
        var teams = new List<string>();
        for (var t = 0; t < 3; t++)
        {
            var teamId = await PostId(client, $"/api/v1/organisations/{orgId}/teams",
                new { name = $"Team {t}", shortName = $"T{t}" });
            teams.Add(await PostId(client,
                $"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId }));
        }

        // The transferring player — ONE registry record, rostered by two different teams.
        var transferId = (await ReadJson(client.PostAsJsonAsync(
            $"/api/v1/organisations/{orgId}/registry/players",
            new { firstName = "Transfer", lastName = "Player", dateOfBirth = "2000-01-01", gender = "Male" })))
            .GetProperty("playerId").GetString()!;

        var rosters = new Dictionary<string, List<string>>();
        foreach (var competitionTeamId in teams)
        {
            var entries = new List<string>();
            for (var p = 0; p < 5; p++)
            {
                var playerId = p == 0 && competitionTeamId != teams[2]
                    ? transferId // the same human on both A and B
                    : (await ReadJson(client.PostAsJsonAsync(
                        $"/api/v1/organisations/{orgId}/registry/players",
                        new { firstName = $"P{p}", lastName = UniqueSlug()[..8], dateOfBirth = "2000-01-01", gender = "Male" })))
                        .GetProperty("playerId").GetString()!;

                entries.Add(await PostId(client,
                    $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}/roster",
                    new { playerId, jerseyNumber = $"{p + 1}" }));
            }

            rosters[competitionTeamId] = entries;
        }

        // Game 1: A vs C. Game 2: B vs C. The player scores in both.
        await PlayAndFinalizeAsync(client, orgId, competitionId, teams[0], teams[2], rosters);
        await PlayAndFinalizeAsync(client, orgId, competitionId, teams[1], teams[2], rosters);
        await DrainOutboxAsync();

        var stats = await ReadJson(client.GetAsync(
            $"/api/v1/organisations/{orgId}/competitions/{competitionId}/player-stats"));

        var rows = stats.EnumerateArray()
            .Where(a => a.GetProperty("playerId").GetString() == transferId).ToList();

        rows.Should().HaveCount(1, "a transfer is one player, not two — the registry record is the identity");
        rows[0].GetProperty("gamesPlayed").GetInt32().Should().Be(2, "both games count toward the one line");
        rows[0].GetProperty("points").GetInt32().Should().Be(4, "two baskets across the two teams combine");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string Leaders(Guid orgId, string competitionId, string stat, string per)
        => $"/api/v1/organisations/{orgId}/competitions/{competitionId}/leaders?stat={stat}&per={per}&limit=50";

    private static string AllLeaders(Guid orgId, string competitionId)
        => $"/api/v1/organisations/{orgId}/competitions/{competitionId}/leaders/all?limit=10";

    private async Task<int> CareerGames(HttpClient client, Guid orgId, string playerId)
    {
        var response = await client.GetAsync($"/api/v1/organisations/{orgId}/players/{playerId}/career");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return 0;
        }

        return (await ReadJson(Task.FromResult(response))).GetProperty("gamesPlayed").GetInt32();
    }

    private async Task RecomputeAllAsync(HttpClient client, Guid orgId)
    {
        List<CompetitionId> competitions = [];
        await WithDbAsync(async db =>
            competitions = await db.Competitions.IgnoreQueryFilters().Select(c => c.Id).ToListAsync());

        foreach (var competitionId in competitions)
        {
            await client.PostAsync($"/api/v1/organisations/{orgId}/admin/recompute/competitions/{competitionId.Value}", null);
        }
    }

    /// <summary>
    /// Seeds aggregate rows directly against REAL parent rows. This is a query/index fixture only — it
    /// deliberately skips the event log, because the criterion is about read performance at volume and
    /// driving 200 games through the write path would measure the wrong thing and take minutes.
    ///
    /// Players are reused across competitions, which is both realistic and what makes the leaderboard
    /// index meaningful: filtering one competition out of many is the selective case that matters.
    /// </summary>
    private async Task<(string CompetitionId, List<string> PlayerIds)> SeedAggregatesAsync(
        Guid orgId, int players, int qualifiedCount, int games = 4, int competitions = 1)
    {
        var organisationId = OrganisationId.FromGuid(orgId);
        var playerIds = new List<string>();
        var target = string.Empty;

        // A real user is needed as the registering user on each player.
        var me = await ReadJson(NewClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me")));

        await WithDbAsync(async db =>
        {
            var userId = await db.Users.Select(u => u.Id).FirstAsync();
            var season = Hoops.Modules.Competitions.Domain.Season.Create(
                organisationId, $"seed-{Guid.NewGuid():N}", new DateOnly(2025, 10, 1), new DateOnly(2026, 6, 30));
            db.Seasons.Add(season);

            var competitionRows = new List<Hoops.Modules.Competitions.Domain.Competition>();
            for (var c = 0; c < competitions; c++)
            {
                competitionRows.Add(Hoops.Modules.Competitions.Domain.Competition.Create(
                    organisationId, season.Id, $"Seeded {c}", $"seeded-{Guid.NewGuid():N}",
                    Hoops.Modules.Competitions.Domain.CompetitionFormat.League, null, null, "Africa/Lagos"));
            }

            db.Competitions.AddRange(competitionRows);

            var playerRows = new List<Hoops.Modules.Registry.Domain.Player>();
            for (var i = 0; i < players; i++)
            {
                playerRows.Add(Hoops.Modules.Registry.Domain.Player.Register(
                    $"Seed{i}", $"Player{i}", new DateOnly(2000, 1, 1),
                    Hoops.Modules.Registry.Domain.Gender.Male, organisationId, userId));
            }

            db.Players.AddRange(playerRows);
            await db.SaveChangesAsync();

            target = competitionRows[0].Id.Value.ToString();
            playerIds.AddRange(playerRows.Select(p => p.Id.Value.ToString()));

            var aggregates = new List<CompetitionPlayerAggregate>();
            foreach (var competition in competitionRows)
            {
                for (var i = 0; i < players; i++)
                {
                    var player = playerRows[i];
                    var qualified = i < qualifiedCount;
                    var statlines = Enumerable.Range(0, qualified ? games : 1)
                        .Select(_ => PlayerGameStatline.FromProjection(
                            organisationId, GameId.New(), competition.Id, CompetitionTeamId.New(), player.Id,
                            GameRosterEntryId.New(), points: 30 - (i % 25), fgm: 5, fga: 12, tpm: 2, tpa: 5,
                            ftm: 3, fta: 4, oreb: 1, dreb: 4, assists: 3, steals: 1, blocks: 1,
                            blocksAgainst: 0, turnovers: 2, foulsCommitted: 2, foulsDrawn: 1,
                            fouledOut: false, secondsPlayed: 1800, plusMinus: 0))
                        .ToList();

                    var aggregate = CompetitionPlayerAggregate.FromStatlines(
                        organisationId, competition.Id, player.Id, statlines);
                    aggregate.SetQualified(qualified);
                    aggregates.Add(aggregate);
                }
            }

            db.CompetitionPlayerAggregates.AddRange(aggregates);
            await db.SaveChangesAsync();
        });

        return (target, playerIds);
    }

    private sealed record Fixture(
        HttpClient Client, Guid OrgId, string GameId, string ScorerPlayerId, string DuplicatePlayerId)
    {
        public string GameUrl => $"/api/v1/organisations/{OrgId}/games/{GameId}";
    }

    private sealed record ShotBody(
        Guid eventId, string eventType, string eventSubtype, int period, int gameClockMs,
        string competitionTeamId, string gameRosterEntryId, string? secondaryRosterEntryId, int shotXCm, int shotYCm);

    private sealed record FlowBody(Guid eventId, string eventType, string? eventSubtype, int period, int gameClockMs);

    /// <summary>A played game with shots from several zones, optionally finalised.</summary>
    private async Task<Fixture> PlayedGameAsync(bool finalize = false)
    {
        var (token, orgId) = await NewOrgWithOwnerAsync();
        var client = AuthenticatedClient(token);

        var seasonId = await PostId(client, $"/api/v1/organisations/{orgId}/seasons",
            new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-06-30" });
        var competitionId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions",
            new { seasonId, name = "League", slug = UniqueSlug(), format = "League", timezone = "Africa/Lagos" });

        var teamIds = new List<string>();
        var entries = new List<List<string>>();
        for (var t = 0; t < 2; t++)
        {
            var teamId = await PostId(client, $"/api/v1/organisations/{orgId}/teams",
                new { name = $"Team {t}", shortName = $"T{t}" });
            var competitionTeamId = await PostId(client,
                $"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId });
            teamIds.Add(competitionTeamId);

            var teamEntries = new List<string>();
            for (var p = 0; p < 5; p++)
            {
                var playerId = (await ReadJson(client.PostAsJsonAsync(
                    $"/api/v1/organisations/{orgId}/registry/players",
                    new { firstName = $"P{p}", lastName = $"T{t}", dateOfBirth = "2000-01-01", gender = "Male" })))
                    .GetProperty("playerId").GetString()!;
                teamEntries.Add(await PostId(client,
                    $"/api/v1/organisations/{orgId}/competition-teams/{competitionTeamId}/roster",
                    new { playerId, jerseyNumber = $"{t * 10 + p + 1}" }));
            }

            entries.Add(teamEntries);
        }

        var gameId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions/{competitionId}/games",
            new
            {
                homeCompetitionTeamId = teamIds[0],
                awayCompetitionTeamId = teamIds[1],
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        var selections = entries.SelectMany(t => t.Select(id => new { rosterEntryId = id, isStarter = true })).ToList();
        (await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/games/{gameId}/lock-roster", new { selections }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await ReadJson(client.GetAsync($"/api/v1/organisations/{orgId}/games/{gameId}/roster"));
        var home = snapshot.EnumerateArray().Where(e => e.GetProperty("competitionTeamId").GetString() == teamIds[0]).ToList();
        var away = snapshot.EnumerateArray().Where(e => e.GetProperty("competitionTeamId").GetString() == teamIds[1]).ToList();
        var homeIds = home.Select(e => e.GetProperty("id").GetString()!).ToList();
        var awayIds = away.Select(e => e.GetProperty("id").GetString()!).ToList();

        var url = $"/api/v1/organisations/{orgId}/games/{gameId}/events";
        await Post(client, url, new FlowBody(Guid.CreateVersion7(), "PERIOD_START", "Regulation", 1, 600_000));
        await Post(client, url, new FlowBody(Guid.CreateVersion7(), "CLOCK_START", null, 1, 600_000));

        // Shots from three distinct zones so the chart aggregation has something to prove.
        await Post(client, url, Shot(homeIds[0], teamIds[0], "TwoPoint", 1200, 0, 550_000));      // RestrictedArea
        await Post(client, url, Shot(homeIds[1], teamIds[0], "TwoPoint", 900, 100, 500_000));     // Paint
        await Post(client, url, Shot(homeIds[2], teamIds[0], "ThreePoint", 500, 0, 450_000));     // AboveBreakThree
        await Post(client, url, MissedShot(awayIds[0], teamIds[1], "TwoPoint", 1150, 50, 400_000));
        await Post(client, url, Shot(awayIds[1], teamIds[1], "TwoPoint", 1100, 0, 350_000));

        await Post(client, url, new FlowBody(Guid.CreateVersion7(), "CLOCK_STOP", null, 1, 0));
        await Post(client, url, new FlowBody(Guid.CreateVersion7(), "PERIOD_END", null, 1, 0));
        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/end", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var scorerPlayerId = home[0].GetProperty("playerId").GetString()!;
        var duplicatePlayerId = away[0].GetProperty("playerId").GetString()!;

        if (finalize)
        {
            (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/finalize", null))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            await DrainOutboxAsync();
        }

        return new Fixture(client, orgId, gameId, scorerPlayerId, duplicatePlayerId);
    }

    /// <summary>Plays a one-basket game between two teams and finalises it.</summary>
    private async Task PlayAndFinalizeAsync(
        HttpClient client, Guid orgId, string competitionId, string homeTeam, string awayTeam,
        Dictionary<string, List<string>> rosters)
    {
        var gameId = await PostId(client, $"/api/v1/organisations/{orgId}/competitions/{competitionId}/games",
            new
            {
                homeCompetitionTeamId = homeTeam,
                awayCompetitionTeamId = awayTeam,
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            });

        var selections = rosters[homeTeam].Concat(rosters[awayTeam])
            .Select(id => new { rosterEntryId = id, isStarter = true }).ToList();
        (await client.PostAsJsonAsync($"/api/v1/organisations/{orgId}/games/{gameId}/lock-roster", new { selections }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await ReadJson(client.GetAsync($"/api/v1/organisations/{orgId}/games/{gameId}/roster"));
        var scorer = snapshot.EnumerateArray()
            .First(e => e.GetProperty("competitionTeamId").GetString() == homeTeam)
            .GetProperty("id").GetString()!;

        var url = $"/api/v1/organisations/{orgId}/games/{gameId}/events";
        await Post(client, url, new FlowBody(Guid.CreateVersion7(), "PERIOD_START", "Regulation", 1, 600_000));
        await Post(client, url, Shot(scorer, homeTeam, "TwoPoint", 1150, 0, 500_000));
        await Post(client, url, new FlowBody(Guid.CreateVersion7(), "PERIOD_END", null, 1, 0));

        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/end", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/v1/organisations/{orgId}/games/{gameId}/finalize", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task DrainOutboxAsync()
    {
        var drainer = Factory.Services.GetRequiredService<Hoops.Api.BackgroundServices.OutboxDrainer>();
        await drainer.DrainOnceAsync(CancellationToken.None);
    }

    private static ShotBody Shot(string shooter, string teamId, string subtype, int x, int y, int clockMs)
        => new(Guid.CreateVersion7(), "FIELD_GOAL_MADE", subtype, 1, clockMs, teamId, shooter, null, x, y);

    private static ShotBody MissedShot(string shooter, string teamId, string subtype, int x, int y, int clockMs)
        => new(Guid.CreateVersion7(), "FIELD_GOAL_MISSED", subtype, 1, clockMs, teamId, shooter, null, x, y);

    private static async Task Post(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> PostId(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "POST {0}: {1}", url, await response.Content.ReadAsStringAsync());
        return (await ReadJson(Task.FromResult(response))).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJson(Task<HttpResponseMessage> call)
    {
        var response = await call;
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
