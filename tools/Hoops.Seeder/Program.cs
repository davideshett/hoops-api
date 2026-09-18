using System.Text.Json;
using Hoops.Seeder;
using Npgsql;

// ─────────────────────────────────────────────────────────────────────────────
// Hoops seeder (implementation-roadmap §3). Drives the running API to build:
//   • 2 organisations sharing 6 players, 1 season + 1 tournament each, 8 teams × 12 players
//   • a mix of identity tiers, 4+ minors with guardian consent
//   • 3 fully recorded games, one with an overridden validation rule
//   • 1 deliberately duplicated player with a PENDING merge proposal, for testing merge
//
// Usage:  dotnet run --project tools/Hoops.Seeder -- [--api http://localhost:5290] [--db <conn>]
// ─────────────────────────────────────────────────────────────────────────────

var apiUrl = Arg("--api") ?? "http://localhost:5290";
var connectionString = Arg("--db") ?? "Host=localhost;Port=5432;Database=hoops;Username=postgres;Password=postgres";

const string Password = "Seed-Password-1";
const string AdminEmail = "admin@hoops.local";
var api = new Api(new Uri(apiUrl));

try
{
    await api.GetAsync("/health");
}
catch (Exception ex)
{
    Fail($"The API is not reachable at {apiUrl}. Start it first: dotnet run --project src/Hoops.Api\n{ex.Message}");
}

// ── 1. Users ─────────────────────────────────────────────────────────────────
Step("Users");
await RegisterIfNew(AdminEmail, "Platform Admin");
await RegisterIfNew("anambra.stats@hoops.local", "Anambra Statistician");
await RegisterIfNew("lagos.stats@hoops.local", "Lagos Statistician");

// Platform admin has no endpoint by design (§16): it is granted out of band. This is that band.
await using (var conn = new NpgsqlConnection(connectionString))
{
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("UPDATE users SET is_system_admin = true WHERE email = @e", conn);
    cmd.Parameters.AddWithValue("e", AdminEmail);
    if (await cmd.ExecuteNonQueryAsync() != 1)
    {
        Fail("Could not grant platform admin — is --db pointing at the same database as the API?");
    }
}

api.Token = await Login(AdminEmail);
Log($"admin: {AdminEmail} / {Password}  (platform admin + owner of both orgs)");

// ── 2. Organisations ─────────────────────────────────────────────────────────
Step("Organisations");
var anambra = await CreateOrg("Anambra Basketball Association", "anambra-bba");
var lagos = await CreateOrg("Lagos Schools League", "lagos-schools");

await api.PostAsync($"/api/v1/organisations/{anambra}/members/invite", new { email = "anambra.stats@hoops.local", role = "Statistician" });
await api.PostAsync($"/api/v1/organisations/{lagos}/members/invite", new { email = "lagos.stats@hoops.local", role = "Statistician" });
Log("statisticians: anambra.stats@hoops.local, lagos.stats@hoops.local  (same password)");

// ── 3. Seasons, venues, competitions ─────────────────────────────────────────
Step("Competitions");
var anambraSeason = await api.CreateAsync($"/api/v1/organisations/{anambra}/seasons",
    new { name = "2025/26", startsOn = "2025-10-01", endsOn = "2026-05-31" });
var lagosSeason = await api.CreateAsync($"/api/v1/organisations/{lagos}/seasons",
    new { name = "2025/26 Academic Year", startsOn = "2025-09-15", endsOn = "2026-07-15" });

var awkaStadium = await api.CreateAsync($"/api/v1/organisations/{anambra}/venues",
    new { name = "Alex Ekwueme Square Indoor Hall", city = "Awka", courtCount = 2, timezone = "Africa/Lagos" });
var rowe = await api.CreateAsync($"/api/v1/organisations/{lagos}/venues",
    new { name = "Rowe Park Sports Centre", city = "Yaba, Lagos", courtCount = 3, timezone = "Africa/Lagos" });

var anambraCup = await api.CreateAsync($"/api/v1/organisations/{anambra}/competitions", new
{
    seasonId = anambraSeason, name = "Anambra State Championship", slug = "state-championship",
    format = "League", category = "Senior Men", startsOn = "2025-11-01", endsOn = "2026-03-31", timezone = "Africa/Lagos",
});
var lagosCup = await api.CreateAsync($"/api/v1/organisations/{lagos}/competitions", new
{
    seasonId = lagosSeason, name = "Inter-Schools U18 Tournament", slug = "u18-tournament",
    format = "League", category = "U18 Boys", startsOn = "2026-01-15", endsOn = "2026-04-30", timezone = "Africa/Lagos",
});
Log($"Anambra State Championship  {anambraCup}");
Log($"Inter-Schools U18 Tournament {lagosCup}");

// ── 4. Players and rosters ───────────────────────────────────────────────────
// Six players are registered in Anambra with a NIN, then ROSTERED in Lagos by searching for that NIN:
// the same registry record on two organisations' rosters, which is what shared careers rest on.
Step("Teams and players");
var random = new Random(2026);
var ninCounter = 10_000_000_000L;
var sharedNins = new List<string>();
var minors = 0;
var anambraTeams = new List<(string CompetitionTeamId, List<string> RosterEntries, List<string> PlayerIds)>();
var lagosTeams = new List<(string CompetitionTeamId, List<string> RosterEntries, List<string> PlayerIds)>();

for (var t = 0; t < 8; t++)
{
    var team = await EnterTeam(anambra, anambraCup, Names.AnambraTeams[t], awkaStadium);
    var entries = new List<string>();
    var playerIds = new List<string>();

    for (var p = 0; p < 12; p++)
    {
        // Adults, born 1996–2006. Every third player carries a NIN; the first six NIN holders on the
        // first two teams will also play in Lagos (as the older end of an U18 side would not — so
        // the shared six are the YOUNGEST Anambra adults, born 2008, which keeps both stories true).
        var isShared = t < 2 && p < 3;
        var dob = isShared ? new DateOnly(2008, 3 + p, 10 + t) : RandomDate(random, 1996, 2006);
        string? nin = isShared || p % 3 == 0 ? (ninCounter++).ToString() : null;
        var player = await RegisterPlayer(anambra, dob, "Male", nin, guardian: dob > new DateOnly(2008, 1, 1));
        if (dob > new DateOnly(2008, 1, 1)) { minors++; }
        if (isShared) { sharedNins.Add(nin!); }

        playerIds.Add(player);
        entries.Add(await api.CreateAsync($"/api/v1/organisations/{anambra}/competition-teams/{team}/roster",
            new { playerId = player, jerseyNumber = JerseyNumber(p), position = Position(p), isCaptain = p == 0 }));

        // Identity tiers: NIN holders get verified (tier 2); every other adult without a NIN gets
        // documented (tier 1); the rest stay at tier 0.
        if (nin is not null)
        {
            await api.PostAsync($"/api/v1/registry/players/{player}/verify-nin", new { nin });
        }
        else if (p % 2 == 0)
        {
            await api.PostAsync($"/api/v1/organisations/{anambra}/registry/players/{player}/dob-evidence",
                new { evidenceType = p % 4 == 0 ? "BirthCertificate" : "Passport" });
        }
    }

    anambraTeams.Add((team, entries, playerIds));
}

for (var t = 0; t < 8; t++)
{
    var team = await EnterTeam(lagos, lagosCup, Names.LagosSchools[t], rowe);
    var entries = new List<string>();
    var playerIds = new List<string>();

    for (var p = 0; p < 12; p++)
    {
        string player;
        if (t < 2 && p < 3)
        {
            // The shared player: found by NIN, never re-created. The NIN goes in the search BODY.
            var found = await api.PostAsync($"/api/v1/organisations/{lagos}/registry/players/search",
                new { nin = sharedNins[(t * 3) + p] });
            player = found[0].GetProperty("playerId").GetString()!;
        }
        else
        {
            // Schoolboys born 2008–2010: minors, so guardian consent is mandatory.
            var dob = RandomDate(random, 2008, 2010);
            player = await RegisterPlayer(lagos, dob, "Male", nin: null, guardian: true);
            minors++;
            if (p % 2 == 1)
            {
                await api.PostAsync($"/api/v1/organisations/{lagos}/registry/players/{player}/dob-evidence",
                    new { evidenceType = "SchoolRecord" });
            }
        }

        playerIds.Add(player);
        entries.Add(await api.CreateAsync($"/api/v1/organisations/{lagos}/competition-teams/{team}/roster",
            new { playerId = player, jerseyNumber = JerseyNumber(p), position = Position(p), isCaptain = p == 0 }));
    }

    lagosTeams.Add((team, entries, playerIds));
}

Log($"16 teams, 186 registry records, 6 shared across both organisations, {minors} minors with guardian consent");

// ── 5. The duplicate ─────────────────────────────────────────────────────────
// The same human registered twice: once in Anambra WITH a NIN, once in Lagos without — so a NIN
// search would never have caught it. Both records sit on rosters. The proposal is left PENDING:
// approving it (as platform admin) is the thing to try.
Step("Duplicate player");
var original = anambraTeams[2].PlayerIds[5];
var originalDetail = await api.GetAsync($"/api/v1/organisations/{anambra}/registry/players/{original}");
var originalName = originalDetail.GetProperty("fullName").GetString()!.Split(' ', 2);
var duplicate = (await api.PostAsync($"/api/v1/organisations/{lagos}/registry/players", new
{
    firstName = originalName[0],
    lastName = originalName[1],
    dateOfBirth = originalDetail.GetProperty("dateOfBirth").GetString(),
    gender = "Male",
    guardianConsent = new { guardianName = "Mrs. Guardian", guardianPhone = "+2348000000000", scopeVersion = "v1" },
})).GetProperty("playerId").GetString()!;
await api.CreateAsync($"/api/v1/organisations/{lagos}/competition-teams/{lagosTeams[7].CompetitionTeamId}/roster",
    new { playerId = duplicate, jerseyNumber = "99", position = "PF", isCaptain = false });
var proposal = await api.CreateAsync($"/api/v1/organisations/{lagos}/registry/merge-proposals",
    new { keepId = original, mergeId = duplicate, evidence = "Same name and date of birth; school confirms he is the Anambra registrant." });
Log($"pending merge proposal {proposal}: keep {original}, merge {duplicate}");
Log($"  approve with: POST /api/v1/registry/merge-proposals/{proposal}/approve  (as {AdminEmail})");

// ── 6. Fixtures and three recorded games ─────────────────────────────────────
Step("Fixtures");
var schedule = await api.PostAsync($"/api/v1/organisations/{anambra}/competitions/{anambraCup}/games/generate/round-robin",
    new { doubleRound = false, firstGameAt = "2025-11-01T16:00:00+01:00" });
var fixtures = schedule.EnumerateArray().Select(g => g.GetProperty("id").GetString()!).ToList();
Log($"{fixtures.Count} round-robin fixtures generated for the Anambra championship");

Step("Recording games");
for (var g = 0; g < 3; g++)
{
    var gameId = fixtures[g];
    var game = await api.GetAsync($"/api/v1/organisations/{anambra}/games/{gameId}");
    var homeTeamId = game.GetProperty("homeCompetitionTeamId").GetString()!;
    var awayTeamId = game.GetProperty("awayCompetitionTeamId").GetString()!;
    var home = anambraTeams.Single(x => x.CompetitionTeamId == homeTeamId);
    var away = anambraTeams.Single(x => x.CompetitionTeamId == awayTeamId);

    await api.PostAsync($"/api/v1/organisations/{anambra}/games/{gameId}/officials", new { fullName = "Chukwudi Anene", role = "Referee" });
    await api.PostAsync($"/api/v1/organisations/{anambra}/games/{gameId}/officials", new { fullName = "Ada Nwankwo", role = "Scorer" });

    var selections = home.RosterEntries.Concat(away.RosterEntries)
        .Select((id, i) => new { rosterEntryId = id, isStarter = i % 12 < 5 }).ToList();
    await api.PostAsync($"/api/v1/organisations/{anambra}/games/{gameId}/lock-roster", new { selections });
    await api.PostAsync($"/api/v1/organisations/{anambra}/games/{gameId}/start", null);

    // Events address the FROZEN game-roster rows, not the competition roster.
    var snapshot = await api.GetAsync($"/api/v1/organisations/{anambra}/games/{gameId}/roster");
    ScriptTeam Side(string teamId) => new(
        teamId,
        snapshot.EnumerateArray().Where(e => e.GetProperty("competitionTeamId").GetString() == teamId && e.GetProperty("isStarter").GetBoolean())
            .Select(e => e.GetProperty("id").GetString()!).ToList(),
        snapshot.EnumerateArray().Where(e => e.GetProperty("competitionTeamId").GetString() == teamId && !e.GetProperty("isStarter").GetBoolean())
            .Select(e => e.GetProperty("id").GetString()!).ToList());

    var script = GameScript.Write(seed: 100 + g, Side(homeTeamId), Side(awayTeamId), periodMs: 600_000, includeOverride: g == 2);
    var eventsUrl = $"/api/v1/organisations/{anambra}/games/{gameId}/events";
    var count = 0;

    foreach (var e in script)
    {
        var body = new
        {
            eventId = Guid.CreateVersion7(),
            e.EventType,
            e.EventSubtype,
            e.Period,
            e.GameClockMs,
            competitionTeamId = e.CompetitionTeamId,
            gameRosterEntryId = e.GameRosterEntryId,
            secondaryRosterEntryId = e.SecondaryRosterEntryId,
            e.ShotXCm,
            e.ShotYCm,
            e.Payload,
        };

        if (e.Override)
        {
            await api.PostAsync($"{eventsUrl}?override=true", body, ("X-Override-Reason", e.OverrideReason!));
        }
        else
        {
            await api.PostAsync(eventsUrl, body);
        }

        count++;
    }

    var final = await api.PostAsync($"/api/v1/organisations/{anambra}/games/{gameId}/end", null);
    await api.PostAsync($"/api/v1/organisations/{anambra}/games/{gameId}/finalize", null);

    var score = final.GetProperty("score");
    Log($"game {g + 1}: {Names.AnambraTeams[anambraTeams.IndexOf(home)]} {score.GetProperty(homeTeamId).GetInt32()} – "
        + $"{score.GetProperty(awayTeamId).GetInt32()} {Names.AnambraTeams[anambraTeams.IndexOf(away)]}  "
        + $"({count} events{(g == 2 ? ", one overridden rule" : string.Empty)})  {gameId}");
}

Step("Done");
Log("The outbox drainer rebuilds standings and leaderboards within a few seconds of finalisation.");
Log($"Swagger: {apiUrl}/swagger   Anambra org: {anambra}   Lagos org: {lagos}");
return;

// ── helpers ──────────────────────────────────────────────────────────────────

async Task RegisterIfNew(string email, string fullName)
{
    try
    {
        await api.PostAsync("/api/v1/auth/register", new { email, password = Password, fullName });
    }
    catch (SeedException ex) when (ex.Message.Contains("409") || ex.Message.Contains("EMAIL_TAKEN"))
    {
        Fail($"{email} already exists — this database has been seeded. Reset it first (see README).");
    }
}

async Task<string> Login(string email)
    => (await api.PostAsync("/api/v1/auth/login", new { email, password = Password })).GetProperty("accessToken").GetString()!;

async Task<string> CreateOrg(string name, string slug)
{
    var created = await api.PostAsync("/api/v1/organisations", new { name, slug, countryCode = "NG", defaultTimezone = "Africa/Lagos" });
    // Creating an org returns a fresh token carrying the new membership claim.
    api.Token = created.GetProperty("accessToken").GetString();
    return created.GetProperty("id").GetString()!;
}

async Task<string> EnterTeam(string orgId, string competitionId, string name, string venueId)
{
    var words = name.Split(' ');
    var teamId = await api.CreateAsync($"/api/v1/organisations/{orgId}/teams", new
    {
        name,
        shortName = words[0],
        // Exactly three letters: initials first, then padded from the first word ("Awka Warriors" → "AWA").
        abbreviation = (string.Concat(words.Select(w => w[0])) + words[0][1..]).ToUpperInvariant()[..3],
        homeVenueId = venueId,
    });
    return await api.CreateAsync($"/api/v1/organisations/{orgId}/competitions/{competitionId}/teams", new { teamId });
}

async Task<string> RegisterPlayer(string orgId, DateOnly dob, string gender, string? nin, bool guardian)
{
    var first = Names.Male[random.Next(Names.Male.Length)];
    var last = Names.Surnames[random.Next(Names.Surnames.Length)];
    var created = await api.PostAsync($"/api/v1/organisations/{orgId}/registry/players", new
    {
        firstName = first,
        lastName = last,
        dateOfBirth = dob.ToString("yyyy-MM-dd"),
        gender,
        nin,
        nationality = "NG",
        guardianConsent = guardian
            ? new { guardianName = $"Mr. {last}", guardianPhone = $"+23480{random.Next(10_000_000, 99_999_999)}", scopeVersion = "v1" }
            : null,
    });
    return created.GetProperty("playerId").GetString()!;
}

static DateOnly RandomDate(Random random, int fromYear, int toYear)
    => new(random.Next(fromYear, toYear + 1), random.Next(1, 13), random.Next(1, 28));

static string JerseyNumber(int p) => p switch { 0 => "0", 1 => "00", _ => (p + 3).ToString() };

static string Position(int p) => (p % 5) switch { 0 => "PG", 1 => "SG", 2 => "SF", 3 => "PF", _ => "C" };

static string? Arg(string name)
{
    var args = Environment.GetCommandLineArgs();
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static void Step(string title) => Console.WriteLine($"\n── {title} ──");

static void Log(string message) => Console.WriteLine($"   {message}");

static void Fail(string message)
{
    Console.Error.WriteLine($"\nSEED FAILED: {message}");
    Environment.Exit(1);
}
