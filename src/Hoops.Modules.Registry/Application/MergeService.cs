using Hoops.Modules.Registry.Application.Abstractions;
using Hoops.Modules.Registry.Contracts;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Registry.Application;

/// <summary>Merge-proposal queue (§5A.5). Orgs propose; platform admins execute. Merges resolve
/// transitively on write, so no lookup is ever two hops.</summary>
public sealed class MergeService : IMergeService
{
    private readonly IMergeProposalRepository _proposals;
    private readonly IPlayerRepository _players;
    private readonly IPlayerOrgLinkRepository _links;
    private readonly IRosterRepository _rosters;
    private readonly IPlayerStatisticsRepointer _statistics;
    private readonly IRegistryLedgerRepository _ledger;
    private readonly IRegistryAuditRepository _audits;
    private readonly IRegistryUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public MergeService(
        IMergeProposalRepository proposals, IPlayerRepository players, IPlayerOrgLinkRepository links,
        IRosterRepository rosters, IRegistryLedgerRepository ledger, IRegistryAuditRepository audits,
        IPlayerStatisticsRepointer statistics, IRegistryUnitOfWork unitOfWork, IClock clock)
    {
        _statistics = statistics;
        _proposals = proposals;
        _players = players;
        _links = links;
        _rosters = rosters;
        _ledger = ledger;
        _audits = audits;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<MergeProposalDto>> ProposeAsync(RegistryCaller caller, CreateMergeProposalRequest request, CancellationToken ct = default)
    {
        if (request.KeepId == request.MergeId)
        {
            return Error.Validation("INVALID_MERGE", "A player cannot be merged into itself.");
        }

        if (await _players.GetAsync(request.KeepId, ct) is null || await _players.GetAsync(request.MergeId, ct) is null)
        {
            return Error.Validation("PLAYER_NOT_FOUND", "Both players must exist.");
        }

        var proposal = MergeProposal.Propose(request.KeepId, request.MergeId, request.Evidence, caller.UserId, caller.OrganisationId, _clock.UtcNow);
        _proposals.Add(proposal);
        await _unitOfWork.SaveChangesAsync(ct);
        return proposal.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MergeProposalDto>>> ListProposalsAsync(CancellationToken ct = default)
    {
        var proposals = await _proposals.ListPendingAsync(ct);
        return Result.Success<IReadOnlyList<MergeProposalDto>>(proposals.Select(p => p.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result> ApproveAsync(RegistryCaller caller, MergeProposalId id, CancellationToken ct = default)
    {
        if (!caller.IsPlatformAdmin)
        {
            return Error.Forbidden("PLATFORM_ADMIN_REQUIRED", "Only a platform administrator can approve a merge.");
        }

        var proposal = await _proposals.GetAsync(id, ct);
        if (proposal is null)
        {
            return Error.NotFound("MERGE_PROPOSAL_NOT_FOUND", "The merge proposal does not exist.");
        }

        if (proposal.Status != MergeProposalStatus.Pending)
        {
            return Error.Conflict("MERGE_ALREADY_DECIDED", "This proposal has already been decided.");
        }

        var executed = await ExecuteMergeAsync(proposal.KeepPlayerId, proposal.MergePlayerId, ct);
        if (executed.IsFailure)
        {
            return executed.Error;
        }

        proposal.Approve(caller.UserId, _clock.UtcNow);
        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.Merge,
            new Dictionary<string, string>(), _clock.UtcNow, proposal.KeepPlayerId, ipAddress: caller.IpAddress));
        await AppendLedgerAsync(LedgerEntryType.Merged, proposal.KeepPlayerId, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RejectAsync(RegistryCaller caller, MergeProposalId id, CancellationToken ct = default)
    {
        if (!caller.IsPlatformAdmin)
        {
            return Error.Forbidden("PLATFORM_ADMIN_REQUIRED", "Only a platform administrator can reject a merge.");
        }

        var proposal = await _proposals.GetAsync(id, ct);
        if (proposal is null)
        {
            return Error.NotFound("MERGE_PROPOSAL_NOT_FOUND", "The merge proposal does not exist.");
        }

        if (proposal.Status != MergeProposalStatus.Pending)
        {
            return Error.Conflict("MERGE_ALREADY_DECIDED", "This proposal has already been decided.");
        }

        proposal.Reject(caller.UserId, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> ExecuteMergeAsync(PlayerId keepId, PlayerId mergeId, CancellationToken ct)
    {
        var survivor = await ResolveSurvivorAsync(keepId, ct);
        if (survivor is null)
        {
            return Error.NotFound("PLAYER_NOT_FOUND", "The surviving player does not exist.");
        }

        // Every record folding in: the merge target and anything already merged into it (transitive).
        var sources = await CollectSourcesAsync(mergeId, survivor.Id, ct);
        var survivorOrgs = (await _links.ListForPlayerAsync(survivor.Id, ct)).Select(l => l.OrganisationId).ToHashSet();

        foreach (var source in sources)
        {
            foreach (var entry in await _rosters.ListForPlayerAsync(source.Id, ct))
            {
                entry.RepointTo(survivor.Id);
            }

            foreach (var link in await _links.ListForPlayerAsync(source.Id, ct))
            {
                if (survivorOrgs.Contains(link.OrganisationId))
                {
                    _links.Remove(link); // survivor already engaged this org — union, don't duplicate
                }
                else
                {
                    link.RepointTo(survivor.Id);
                    survivorOrgs.Add(link.OrganisationId);
                }
            }

            survivor.RaiseTierTo(source.IdentityTier);
            source.MergeInto(survivor.Id);
        }

        // Carry the losing records' derived statistics across too. Without this the survivor's career
        // page would silently lose every game the duplicate played — which is exactly the history the
        // registry exists to preserve.
        if (sources.Count > 0)
        {
            await _statistics.RepointStatlinesAsync(sources.Select(s => s.Id).ToList(), survivor.Id, ct);
        }

        return Result.Success();
    }

    private async Task<Player?> ResolveSurvivorAsync(PlayerId id, CancellationToken ct)
    {
        var player = await _players.GetAsync(id, ct);
        while (player?.MergedIntoId is { } next)
        {
            player = await _players.GetAsync(next, ct);
        }

        return player;
    }

    private async Task<List<Player>> CollectSourcesAsync(PlayerId mergeId, PlayerId survivorId, CancellationToken ct)
    {
        var sources = new List<Player>();
        var seen = new HashSet<PlayerId>();
        var queue = new Queue<PlayerId>();
        queue.Enqueue(mergeId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (id == survivorId || !seen.Add(id))
            {
                continue;
            }

            var player = await _players.GetAsync(id, ct);
            if (player is null)
            {
                continue;
            }

            sources.Add(player);
            foreach (var child in await _players.ListMergedIntoAsync(id, ct))
            {
                queue.Enqueue(child.Id);
            }
        }

        return sources;
    }

    private async Task AppendLedgerAsync(LedgerEntryType type, PlayerId playerId, CancellationToken ct)
    {
        var previous = (await _ledger.GetLastAsync(ct))?.EntryHash ?? RegistryLedgerEntry.Genesis;
        _ledger.Add(RegistryLedgerEntry.Append(previous, type, playerId, _clock.UtcNow));
    }
}
