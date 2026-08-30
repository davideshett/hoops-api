using Hoops.Api.Auth;
using Hoops.Modules.Registry.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Registry;

/// <summary>
/// Platform-administrator registry operations (ADR-003): NIN verification, anonymisation, the merge
/// queue, ledger verification, and metrics. Every action requires the platform-admin claim.
/// </summary>
[Route("api/v1/registry")]
public sealed class PlatformRegistryController : RegistryControllerBase
{
    private readonly IPlayerRegistryService _registry;
    private readonly IMergeService _merge;

    /// <summary>Creates the controller.</summary>
    public PlatformRegistryController(IPlayerRegistryService registry, IMergeService merge, ICurrentUser currentUser)
        : base(currentUser)
    {
        _registry = registry;
        _merge = merge;
    }

    /// <summary>Verifies a player's NIN through the (stubbed) licensed provider, moving them to tier 2.</summary>
    /// <response code="200">The updated player.</response>
    /// <response code="403">The caller is not a platform administrator.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpPost("players/{playerId:guid}/verify-nin")]
    [ProducesResponseType(typeof(PlayerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerSummaryDto>> VerifyNin(Guid playerId, [FromBody] VerifyNinRequest request, CancellationToken ct)
        => Ok(await _registry.VerifyNinAsync(PlatformCaller(), PlayerId.FromGuid(playerId), request, ct));

    /// <summary>Anonymises a player for NDPA erasure — clears identity, retains id and statlines.</summary>
    /// <response code="204">The player was anonymised.</response>
    /// <response code="403">The caller is not a platform administrator.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpPost("players/{playerId:guid}/anonymise")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Anonymise(Guid playerId, CancellationToken ct)
        => NoContent(await _registry.AnonymiseAsync(PlatformCaller(), PlayerId.FromGuid(playerId), ct));

    /// <summary>Resolves an eligibility flag.</summary>
    /// <response code="204">The flag was resolved.</response>
    /// <response code="404">The flag does not exist.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpPatch("flags/{flagId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResolveFlag(Guid flagId, CancellationToken ct)
        => NoContent(await _registry.ResolveFlagAsync(EligibilityFlagId.FromGuid(flagId), ct));

    /// <summary>Lists pending merge proposals.</summary>
    /// <response code="200">The pending proposals.</response>
    /// <response code="403">The caller is not a platform administrator.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpGet("merge-proposals")]
    [ProducesResponseType(typeof(IReadOnlyList<MergeProposalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MergeProposalDto>>> ListProposals(CancellationToken ct)
        => Ok(await _merge.ListProposalsAsync(ct));

    /// <summary>Approves and executes a merge — platform admin only.</summary>
    /// <response code="204">The merge was executed.</response>
    /// <response code="403">The caller is not a platform administrator.</response>
    /// <response code="404">The proposal does not exist.</response>
    /// <response code="409">The proposal was already decided.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpPost("merge-proposals/{proposalId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Approve(Guid proposalId, CancellationToken ct)
        => NoContent(await _merge.ApproveAsync(PlatformCaller(), MergeProposalId.FromGuid(proposalId), ct));

    /// <summary>Rejects a merge proposal — platform admin only.</summary>
    /// <response code="204">The proposal was rejected.</response>
    /// <response code="403">The caller is not a platform administrator.</response>
    /// <response code="404">The proposal does not exist.</response>
    /// <response code="409">The proposal was already decided.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpPost("merge-proposals/{proposalId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reject(Guid proposalId, CancellationToken ct)
        => NoContent(await _merge.RejectAsync(PlatformCaller(), MergeProposalId.FromGuid(proposalId), ct));

    /// <summary>Recomputes and validates the provenance ledger's hash chain.</summary>
    /// <response code="200">The verification result.</response>
    /// <response code="403">The caller is not a platform administrator.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpGet("ledger/verify")]
    [ProducesResponseType(typeof(LedgerVerificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LedgerVerificationDto>> VerifyLedger(CancellationToken ct)
        => Ok(await _registry.VerifyLedgerAsync(ct));

    /// <summary>Registry coverage and tier-distribution metrics.</summary>
    /// <response code="200">The metrics.</response>
    /// <response code="403">The caller is not a platform administrator.</response>
    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(RegistryMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RegistryMetricsDto>> Metrics(CancellationToken ct)
        => Ok(await _registry.GetMetricsAsync(ct));
}
