using System.Text.Json;
using System.Text.Json.Serialization;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.GameRecording.Domain.Projection;
using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.UnitTests.GameRecording;

/// <summary>A player in a golden fixture. <c>Team</c> is "H" or "A".</summary>
public sealed record GoldenPlayer(int Id, string Team, bool Starter);

/// <summary>
/// One event in a golden fixture. Short names keep the hand-authored JSON readable.
/// <c>P</c>/<c>S</c> are primary/secondary player refs; <c>Team</c> attributes team-level events.
/// </summary>
public sealed record GoldenEvent(
    long Seq,
    string Type,
    int Period,
    int Clock,
    string? Sub = null,
    int? P = null,
    int? S = null,
    string? Team = null,
    int? X = null,
    int? Y = null,
    bool Voided = false);

/// <summary>A complete golden game: its roster and its event log.</summary>
public sealed record GoldenGame(IReadOnlyList<GoldenPlayer> Players, IReadOnlyList<GoldenEvent> Events);

/// <summary>One expected player statline in a golden fixture.</summary>
public sealed record ExpectedLine(
    int Player, int Pts, int Fgm, int Fga, int Tpm, int Tpa, int Ftm, int Fta,
    int Oreb, int Dreb, int Ast, int Stl, int Blk, int Blka, int Tov, int Pf, int Fd,
    bool FouledOut, int PlusMinus);

/// <summary>The expected projection for a golden game.</summary>
public sealed record ExpectedProjection(
    int HomePoints, int AwayPoints, IReadOnlyList<ExpectedLine> Lines);

/// <summary>Loads golden fixtures and turns them into projector inputs.</summary>
public static class GoldenFiles
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The home team in every golden fixture.</summary>
    public static readonly CompetitionTeamId Home = CompetitionTeamId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000a1"));

    /// <summary>The away team in every golden fixture.</summary>
    public static readonly CompetitionTeamId Away = CompetitionTeamId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000a2"));

    /// <summary>The directory holding golden fixtures.</summary>
    public static string Directory(string name) => Path.Combine(AppContext.BaseDirectory, "GoldenFiles", name);

    /// <summary>Reads a game's <c>events.json</c>.</summary>
    public static GoldenGame LoadGame(string name)
        => JsonSerializer.Deserialize<GoldenGame>(File.ReadAllText(Path.Combine(Directory(name), "events.json")), Options)!;

    /// <summary>Reads a game's <c>expected.json</c>.</summary>
    public static ExpectedProjection LoadExpected(string name)
        => JsonSerializer.Deserialize<ExpectedProjection>(File.ReadAllText(Path.Combine(Directory(name), "expected.json")), Options)!;

    /// <summary>A deterministic roster-entry id for a fixture player ref.</summary>
    public static GameRosterEntryId Roster(int playerRef)
        => GameRosterEntryId.FromGuid(new Guid($"00000000-0000-0000-0000-{playerRef:D12}"));

    /// <summary>A deterministic player id for a fixture player ref.</summary>
    public static PlayerId Player(int playerRef)
        => PlayerId.FromGuid(new Guid($"00000000-0000-0000-0001-{playerRef:D12}"));

    /// <summary>Builds the projector context for a golden game.</summary>
    public static GameContext ToContext(this GoldenGame game, RuleSet? ruleSet = null)
        => new(
            GameId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000f1")),
            Home,
            Away,
            game.Players.Select(p => new GamePlayer(Roster(p.Id), TeamOf(p.Team), Player(p.Id), p.Starter)).ToList(),
            ruleSet ?? RuleSet.Fiba());

    /// <summary>Materialises a golden game's events as domain events.</summary>
    public static IReadOnlyList<GameEvent> ToEvents(this GoldenGame game)
    {
        var org = OrganisationId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000b1"));
        var gameId = GameId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000f1"));
        var user = UserId.FromGuid(new Guid("00000000-0000-0000-0000-0000000000c1"));
        var at = DateTimeOffset.UnixEpoch;

        return game.Events.Select(e =>
        {
            var domain = GameEvent.Record(
                eventId: new Guid($"00000000-0000-0000-0002-{e.Seq:D12}"),
                organisationId: org,
                gameId: gameId,
                sequence: e.Seq,
                eventType: e.Type,
                eventSubtype: e.Sub,
                period: e.Period,
                gameClockMs: e.Clock,
                recordedAt: at,
                recordedByUserId: user,
                competitionTeamId: e.Team is null ? null : TeamOf(e.Team),
                gameRosterEntryId: e.P.HasValue ? Roster(e.P.Value) : null,
                secondaryRosterEntryId: e.S.HasValue ? Roster(e.S.Value) : null,
                shotXCm: e.X,
                shotYCm: e.Y);

            if (e.Voided)
            {
                domain.Void();
            }

            return domain;
        }).ToList();
    }

    private static CompetitionTeamId TeamOf(string team)
        => string.Equals(team, "H", StringComparison.OrdinalIgnoreCase) ? Home : Away;
}
