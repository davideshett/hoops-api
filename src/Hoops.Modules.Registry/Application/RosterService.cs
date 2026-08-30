using Hoops.Modules.Registry.Application.Abstractions;
using Hoops.Modules.Registry.Contracts;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Registry.Application;

/// <summary>
/// Roster entries — a player's place in a competition-team squad. Built ONLY from an existing
/// <see cref="PlayerId"/> resolved from the registry (§5A.1), never from a free-text name.
/// </summary>
public sealed class RosterService : IRosterService
{
    private readonly IRosterRepository _rosters;
    private readonly IPlayerRepository _players;
    private readonly IPlayerOrgLinkRepository _links;
    private readonly IRegistryUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public RosterService(
        IRosterRepository rosters, IPlayerRepository players, IPlayerOrgLinkRepository links,
        IRegistryUnitOfWork unitOfWork, IClock clock)
    {
        _rosters = rosters;
        _players = players;
        _links = links;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<RosterEntryDto>> RegisterAsync(
        RegistryCaller caller, CompetitionTeamId competitionTeamId, RegisterRosterEntryRequest request, CancellationToken ct = default)
    {
        // A roster entry must reference a real registry player id — never a typed name.
        if (request.PlayerId.Value == Guid.Empty)
        {
            return Error.Validation("PLAYER_ID_REQUIRED", "A roster entry requires a playerId resolved from the registry, not a name.");
        }

        if (!await _rosters.CompetitionTeamExistsAsync(competitionTeamId, ct))
        {
            return Error.NotFound("COMPETITION_TEAM_NOT_FOUND", "The competition team does not exist.");
        }

        var player = await _players.GetAsync(request.PlayerId, ct);
        if (player is null)
        {
            return Error.Validation("PLAYER_NOT_FOUND", "The referenced player does not exist in the registry.");
        }

        if (await _rosters.PlayerOnRosterAsync(competitionTeamId, request.PlayerId, ct))
        {
            return Error.Conflict("PLAYER_ALREADY_ROSTERED", "That player is already on this squad.");
        }

        if (await _rosters.JerseyTakenAsync(competitionTeamId, request.JerseyNumber.Trim(), ct))
        {
            return Error.Conflict("JERSEY_TAKEN", "That jersey number is already in use on this squad.");
        }

        var now = _clock.UtcNow;
        var entry = RosterEntry.Register(
            caller.OrganisationId, competitionTeamId, request.PlayerId, (int)player.IdentityTier,
            request.JerseyNumber, now, request.Position, request.IsCaptain);
        _rosters.Add(entry);

        // Provenance: record (or touch) the org's engagement with this player.
        var link = await _links.GetAsync(request.PlayerId, caller.OrganisationId, ct);
        if (link is null)
        {
            _links.Add(PlayerOrgLink.Create(request.PlayerId, caller.OrganisationId, now));
        }
        else
        {
            link.Touch(now);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return entry.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RosterEntryDto>>> ListAsync(CompetitionTeamId competitionTeamId, CancellationToken ct = default)
    {
        var entries = await _rosters.ListForCompetitionTeamAsync(competitionTeamId, ct);
        return Result.Success<IReadOnlyList<RosterEntryDto>>(entries.Select(e => e.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<RosterEntryDto>> UpdateAsync(RosterEntryId id, UpdateRosterEntryRequest request, CancellationToken ct = default)
    {
        var entry = await _rosters.GetAsync(id, ct);
        if (entry is null)
        {
            return Error.NotFound("ROSTER_ENTRY_NOT_FOUND", "The roster entry does not exist.");
        }

        RosterEntryStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse(request.Status, ignoreCase: true, out RosterEntryStatus parsed) || !Enum.IsDefined(parsed))
            {
                return Error.Validation("INVALID_STATUS", $"'{request.Status}' is not a valid roster status.");
            }

            status = parsed;
        }

        if (!string.IsNullOrWhiteSpace(request.JerseyNumber)
            && request.JerseyNumber.Trim() != entry.JerseyNumber
            && await _rosters.JerseyTakenAsync(entry.CompetitionTeamId, request.JerseyNumber.Trim(), ct))
        {
            return Error.Conflict("JERSEY_TAKEN", "That jersey number is already in use on this squad.");
        }

        entry.Update(request.JerseyNumber, request.Position, request.IsCaptain, status);
        await _unitOfWork.SaveChangesAsync(ct);
        return entry.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(RosterEntryId id, CancellationToken ct = default)
    {
        var entry = await _rosters.GetAsync(id, ct);
        if (entry is null)
        {
            return Error.NotFound("ROSTER_ENTRY_NOT_FOUND", "The roster entry does not exist.");
        }

        // Soft-remove: sets status Removed, which frees the jersey number (partial unique index).
        entry.Update(null, null, null, RosterEntryStatus.Removed);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
