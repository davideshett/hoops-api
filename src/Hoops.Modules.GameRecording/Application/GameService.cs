using Hoops.Modules.GameRecording.Application.Abstractions;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.Modules.GameRecording.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.GameRecording.Application;

/// <summary>Fixtures: creation, scheduling, generation, officials, and roster lock (§6 → RosterLocked).</summary>
public sealed class GameService : IGameService
{
    private readonly IGameRepository _games;
    private readonly IGameRosterRepository _gameRosters;
    private readonly IGameOfficialRepository _officials;
    private readonly IRosterSnapshotSource _rosters;
    private readonly IGameCompetitionSource _competitions;
    private readonly IGameLabelSource _labels;
    private readonly IGameRecordingUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public GameService(
        IGameRepository games, IGameRosterRepository gameRosters, IGameOfficialRepository officials,
        IRosterSnapshotSource rosters, IGameCompetitionSource competitions, IGameLabelSource labels,
        IGameRecordingUnitOfWork unitOfWork, IClock clock)
    {
        _games = games;
        _gameRosters = gameRosters;
        _officials = officials;
        _rosters = rosters;
        _competitions = competitions;
        _labels = labels;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GameDto>>> ListAsync(CompetitionId competitionId, StageId? stageId, string? status, CancellationToken ct = default)
    {
        GameStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<GameStatus>(status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Error.Validation("INVALID_STATUS", $"'{status}' is not a valid game status.");
            }

            statusFilter = parsed;
        }

        var games = await _games.ListForCompetitionAsync(competitionId, stageId, statusFilter, ct);
        return Result.Success<IReadOnlyList<GameDto>>(games.Select(g => g.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<GameDto>> CreateAsync(OrganisationId organisationId, CompetitionId competitionId, CreateGameRequest request, CancellationToken ct = default)
    {
        if (!await _competitions.CompetitionExistsAsync(competitionId, ct))
        {
            return CompetitionNotFound();
        }

        if (!await _competitions.IsTeamInCompetitionAsync(request.HomeCompetitionTeamId, competitionId, ct)
            || !await _competitions.IsTeamInCompetitionAsync(request.AwayCompetitionTeamId, competitionId, ct))
        {
            return Error.Validation("TEAM_NOT_IN_COMPETITION", "Both teams must be entered in this competition.");
        }

        Game game;
        try
        {
            game = Game.Schedule(organisationId, competitionId, request.HomeCompetitionTeamId, request.AwayCompetitionTeamId,
                request.ScheduledAt, request.StageId, request.GroupId, request.VenueId);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("INVALID_FIXTURE", ex.Message);
        }

        _games.Add(game);
        await _unitOfWork.SaveChangesAsync(ct);
        return game.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<GameDto>> GetAsync(GameId id, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(id, ct);
        return game is null ? GameNotFound() : game.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<GameDto>> RescheduleAsync(GameId id, RescheduleGameRequest request, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(id, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (!game.Reschedule(request.ScheduledAt, request.VenueId, request.ClearVenue))
        {
            return Error.Conflict("GAME_NOT_RESCHEDULABLE", "A game can only be rescheduled before its roster is locked.");
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return game.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(GameId id, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(id, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (game.Status != GameStatus.Scheduled)
        {
            return Error.Conflict("GAME_NOT_DELETABLE", "Only a scheduled game can be deleted.");
        }

        _games.Remove(game);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public Task<Result<GameDto>> PostponeAsync(GameId id, CancellationToken ct = default)
        => TransitionAsync(id, g => g.Postpone(), "GAME_NOT_POSTPONABLE", "Only a scheduled game can be postponed.", ct);

    /// <inheritdoc />
    public Task<Result<GameDto>> CancelAsync(GameId id, CancellationToken ct = default)
        => TransitionAsync(id, g => g.Cancel(), "GAME_NOT_CANCELLABLE", "Only a scheduled or postponed game can be cancelled.", ct);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GameDto>>> GenerateRoundRobinAsync(
        OrganisationId organisationId, CompetitionId competitionId, GenerateRoundRobinRequest request, CancellationToken ct = default)
    {
        var teams = await LoadTeamsForGenerationAsync(competitionId, ordered: false, ct);
        if (teams.IsFailure)
        {
            return teams.Error;
        }

        var pairs = FixtureGenerator.RoundRobin(teams.Value, request.DoubleRound);
        return await PersistGeneratedAsync(organisationId, competitionId, request.StageId, request.FirstGameAt, pairs, ct);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GameDto>>> GenerateKnockoutAsync(
        OrganisationId organisationId, CompetitionId competitionId, GenerateKnockoutRequest request, CancellationToken ct = default)
    {
        var teams = await LoadTeamsForGenerationAsync(competitionId, ordered: true, ct);
        if (teams.IsFailure)
        {
            return teams.Error;
        }

        var pairs = FixtureGenerator.Knockout(teams.Value);
        return await PersistGeneratedAsync(organisationId, competitionId, request.StageId, request.FirstGameAt, pairs, ct);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GameOfficialDto>>> ListOfficialsAsync(GameId id, CancellationToken ct = default)
    {
        var officials = await _officials.ListForGameAsync(id, ct);
        return Result.Success<IReadOnlyList<GameOfficialDto>>(officials.Select(o => o.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<GameOfficialDto>> AssignOfficialAsync(OrganisationId organisationId, GameId id, AssignOfficialRequest request, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(id, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (!Enum.TryParse<OfficialRole>(request.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
        {
            return Error.Validation("INVALID_OFFICIAL_ROLE", $"'{request.Role}' is not a valid official role.");
        }

        var official = GameOfficial.Assign(organisationId, id, request.FullName, role);
        _officials.Add(official);
        await _unitOfWork.SaveChangesAsync(ct);
        return official.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<GameSetupDto>> GetSetupAsync(GameId id, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(id, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        var ruleSet = game.RuleSetSnapshot ?? await _competitions.GetRuleSetAsync(game.CompetitionId, ct);
        if (ruleSet is null)
        {
            return CompetitionNotFound();
        }

        var teamIds = new[] { game.HomeCompetitionTeamId, game.AwayCompetitionTeamId };
        var rosters = new Dictionary<CompetitionTeamId, IReadOnlyList<RosterSnapshotRow>>();
        foreach (var teamId in teamIds)
        {
            rosters[teamId] = await _rosters.ListActiveForTeamAsync(teamId, ct);
        }

        // Two lookups for the whole screen, so the app is not left making one call per player.
        var names = await _labels.GetPlayerNamesAsync(rosters.Values.SelectMany(r => r).Select(r => r.PlayerId).ToList(), ct);
        var labels = await _labels.GetTeamLabelsAsync(teamIds, ct);

        var teams = teamIds.Select(teamId =>
        {
            var label = labels.GetValueOrDefault(teamId) ?? new TeamLabel("Unknown team", "?", null);
            return new TeamRosterDto(teamId, label.Name, label.ShortName, label.Abbreviation, rosters[teamId].Select(p =>
                new AvailablePlayerDto(
                    p.RosterEntryId, p.PlayerId, names.GetValueOrDefault(p.PlayerId, "Unknown player"),
                    p.JerseyNumber, p.Position, p.IsCaptain, p.VerifiedTier)).ToList());
        }).ToList();

        return new GameSetupDto(game.ToDto(), ruleSet, teams);
    }

    /// <inheritdoc />
    public async Task<Result<GameDto>> LockRosterAsync(GameId id, LockRosterRequest request, CancellationToken ct = default)
    {
        var game = await _games.GetAsync(id, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (game.Status != GameStatus.Scheduled)
        {
            return Error.Conflict("GAME_NOT_LOCKABLE", "Only a scheduled game's roster can be locked.");
        }

        var ruleSet = await _competitions.GetRuleSetAsync(game.CompetitionId, ct);
        if (ruleSet is null)
        {
            return CompetitionNotFound();
        }

        var ids = request.Selections.Select(s => s.RosterEntryId).ToList();
        var rows = (await _rosters.GetEntriesByIdsAsync(ids, ct)).ToDictionary(r => r.RosterEntryId);

        // Every selected entry must exist and belong to one of the two teams.
        foreach (var selection in request.Selections)
        {
            if (!rows.TryGetValue(selection.RosterEntryId, out var row))
            {
                return Error.Validation("ROSTER_ENTRY_NOT_FOUND", "A selected roster entry does not exist.");
            }

            if (row.CompetitionTeamId != game.HomeCompetitionTeamId && row.CompetitionTeamId != game.AwayCompetitionTeamId)
            {
                return Error.Validation("ROSTER_ENTRY_WRONG_TEAM", "A selected roster entry does not belong to either team in this game.");
            }
        }

        foreach (var teamId in new[] { game.HomeCompetitionTeamId, game.AwayCompetitionTeamId })
        {
            var included = request.Selections
                .Select(s => (Selection: s, Row: rows[s.RosterEntryId]))
                .Where(x => x.Row.CompetitionTeamId == teamId && x.Row.IsActive)
                .ToList();

            if (included.Count < ruleSet.MinRosterSize)
            {
                return Error.Validation("ROSTER_TOO_SMALL",
                    $"Team {teamId.Value} has {included.Count} active players; at least {ruleSet.MinRosterSize} are required.");
            }

            var starters = included.Count(x => x.Selection.IsStarter);
            if (starters != ruleSet.PlayersOnCourt)
            {
                return Error.Validation("WRONG_STARTER_COUNT",
                    $"Team {teamId.Value} must mark exactly {ruleSet.PlayersOnCourt} starters, not {starters}.");
            }
        }

        // Freeze the snapshot: copy each selected entry as it is right now.
        var snapshot = request.Selections.Select(s =>
        {
            var row = rows[s.RosterEntryId];
            return GameRosterEntry.Capture(game.OrganisationId, game.Id, row.CompetitionTeamId, row.PlayerId,
                row.JerseyNumber, row.Position, s.IsStarter, row.IsCaptain);
        }).ToList();

        _gameRosters.AddRange(snapshot);
        game.LockRoster(ruleSet, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return game.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<GameRosterEntryDto>>> GetGameRosterAsync(GameId id, CancellationToken ct = default)
    {
        var entries = await _gameRosters.ListForGameAsync(id, ct);
        var names = await _labels.GetPlayerNamesAsync(entries.Select(e => e.PlayerId).Distinct().ToList(), ct);
        return Result.Success<IReadOnlyList<GameRosterEntryDto>>(
            entries.Select(e => e.ToDto(names.GetValueOrDefault(e.PlayerId, "Unknown player"))).ToList());
    }

    private async Task<Result<GameDto>> TransitionAsync(GameId id, Func<Game, bool> transition, string code, string message, CancellationToken ct)
    {
        var game = await _games.GetAsync(id, ct);
        if (game is null)
        {
            return GameNotFound();
        }

        if (!transition(game))
        {
            return Error.Conflict(code, message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return game.ToDto();
    }

    private async Task<Result<IReadOnlyList<CompetitionTeamId>>> LoadTeamsForGenerationAsync(CompetitionId competitionId, bool ordered, CancellationToken ct)
    {
        if (!await _competitions.CompetitionExistsAsync(competitionId, ct))
        {
            return CompetitionNotFound();
        }

        var entered = await _competitions.ListEnteredTeamsAsync(competitionId, ct);
        if (entered.Count < 2)
        {
            return Error.Validation("NOT_ENOUGH_TEAMS", "At least two teams must be entered to generate fixtures.");
        }

        var teams = ordered
            ? entered.OrderBy(t => t.Seed ?? int.MaxValue).ThenBy(t => t.Id.Value).Select(t => t.Id).ToList()
            : entered.OrderBy(t => t.Id.Value).Select(t => t.Id).ToList();

        return Result.Success<IReadOnlyList<CompetitionTeamId>>(teams);
    }

    private async Task<Result<IReadOnlyList<GameDto>>> PersistGeneratedAsync(
        OrganisationId organisationId, CompetitionId competitionId, StageId? stageId, DateTimeOffset firstGameAt,
        IReadOnlyList<(CompetitionTeamId Home, CompetitionTeamId Away)> pairs, CancellationToken ct)
    {
        var games = pairs.Select(p =>
            Game.Schedule(organisationId, competitionId, p.Home, p.Away, firstGameAt, stageId)).ToList();

        _games.AddRange(games);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<GameDto>>(games.Select(g => g.ToDto()).ToList());
    }

    private static Error GameNotFound() => Error.NotFound("GAME_NOT_FOUND", "The game does not exist.");

    private static Error CompetitionNotFound() => Error.NotFound("COMPETITION_NOT_FOUND", "The competition does not exist.");
}
