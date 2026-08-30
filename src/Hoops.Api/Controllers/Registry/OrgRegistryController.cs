using Hoops.Api.Auth;
using Hoops.Modules.Registry.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Registry;

/// <summary>
/// Player registry operations available to organisation members (search, registration, evidence,
/// photos, flags, merge proposals). Org-scoped so the acting organisation is well-defined for the
/// audit trail. NINs are accepted in request bodies only, never in a URL.
/// </summary>
[Route("api/v1/organisations/{orgId:guid}/registry")]
public sealed class OrgRegistryController : RegistryControllerBase
{
    private readonly IPlayerRegistryService _registry;
    private readonly IMergeService _merge;

    /// <summary>Creates the controller.</summary>
    public OrgRegistryController(IPlayerRegistryService registry, IMergeService merge, ICurrentUser currentUser)
        : base(currentUser)
    {
        _registry = registry;
        _merge = merge;
    }

    /// <summary>Searches the registry (NIN, or surname + DOB/birth year). Audited; capped at 25 rows.</summary>
    /// <response code="200">Up to 25 matching players (limited projection).</response>
    /// <response code="400">The search was too broad.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpPost("players/search")]
    [ProducesResponseType(typeof(IReadOnlyList<PlayerSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PlayerSummaryDto>>> Search(Guid orgId, [FromBody] PlayerSearchRequest request, CancellationToken ct)
        => Ok(await _registry.SearchAsync(CallerFor(orgId), request, ct));

    /// <summary>Registers a player (only via roster registration). A NIN collision returns 409.</summary>
    /// <response code="201">The created player (limited projection).</response>
    /// <response code="400">Invalid payload, or a minor without guardian consent.</response>
    /// <response code="409">A player with this NIN already exists.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("players")]
    [ProducesResponseType(typeof(PlayerSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayerSummaryDto>> Create(Guid orgId, [FromBody] CreatePlayerRequest request, CancellationToken ct)
        => Created(await _registry.CreateAsync(CallerFor(orgId), request, ct));

    /// <summary>Fetches a player's limited projection.</summary>
    /// <response code="200">The player.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}")]
    [ProducesResponseType(typeof(PlayerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerSummaryDto>> Get(Guid orgId, Guid playerId, CancellationToken ct)
        => Ok(await _registry.GetAsync(PlayerId.FromGuid(playerId), ct));

    /// <summary>Fetches sensitive fields for a player. Audited as ViewSensitive.</summary>
    /// <response code="200">The sensitive projection.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}/sensitive")]
    [ProducesResponseType(typeof(PlayerSensitiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerSensitiveDto>> GetSensitive(Guid orgId, Guid playerId, CancellationToken ct)
        => Ok(await _registry.GetSensitiveAsync(CallerFor(orgId), PlayerId.FromGuid(playerId), ct));

    /// <summary>Updates a player's core identity. Blocked once the NIN is verified.</summary>
    /// <response code="200">The updated player.</response>
    /// <response code="404">The player does not exist.</response>
    /// <response code="409">Identity is locked because the NIN is verified.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPatch("players/{playerId:guid}")]
    [ProducesResponseType(typeof(PlayerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayerSummaryDto>> Update(Guid orgId, Guid playerId, [FromBody] UpdatePlayerRequest request, CancellationToken ct)
        => Ok(await _registry.UpdateAsync(CallerFor(orgId), PlayerId.FromGuid(playerId), request, ct));

    /// <summary>Issues a presigned upload URL for a player's photo (private bucket).</summary>
    /// <response code="200">The presigned upload URL and expiry.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("players/{playerId:guid}/photo")]
    [ProducesResponseType(typeof(PresignedUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PresignedUrlDto>> UploadPhoto(Guid orgId, Guid playerId, [FromQuery] string? contentType, CancellationToken ct)
        => Ok(await _registry.CreatePhotoUploadAsync(PlayerId.FromGuid(playerId), contentType, ct));

    /// <summary>Issues a short-lived presigned read URL for a player's photo.</summary>
    /// <response code="200">The presigned read URL and expiry.</response>
    /// <response code="404">The player or photo does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}/photo")]
    [ProducesResponseType(typeof(PresignedUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PresignedUrlDto>> GetPhoto(Guid orgId, Guid playerId, CancellationToken ct)
        => Ok(await _registry.GetPhotoAsync(PlayerId.FromGuid(playerId), ct));

    /// <summary>The player's cross-organisation history.</summary>
    /// <response code="200">The history.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}/history")]
    [ProducesResponseType(typeof(PlayerHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerHistoryDto>> History(Guid orgId, Guid playerId, CancellationToken ct)
        => Ok(await _registry.GetHistoryAsync(PlayerId.FromGuid(playerId), ct));

    /// <summary>Lists a player's eligibility flags.</summary>
    /// <response code="200">The flags.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}/eligibility")]
    [ProducesResponseType(typeof(IReadOnlyList<EligibilityFlagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<EligibilityFlagDto>>> Eligibility(Guid orgId, Guid playerId, CancellationToken ct)
        => Ok(await _registry.ListFlagsAsync(PlayerId.FromGuid(playerId), ct));

    /// <summary>Raises an eligibility flag that follows the player.</summary>
    /// <response code="201">The raised flag.</response>
    /// <response code="400">Invalid flag type or scope.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("players/{playerId:guid}/flags")]
    [ProducesResponseType(typeof(EligibilityFlagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EligibilityFlagDto>> RaiseFlag(Guid orgId, Guid playerId, [FromBody] RaiseFlagRequest request, CancellationToken ct)
        => Created(await _registry.RaiseFlagAsync(CallerFor(orgId), PlayerId.FromGuid(playerId), request, ct));

    /// <summary>Records reviewed DOB evidence, moving the identity tier.</summary>
    /// <response code="200">The updated player.</response>
    /// <response code="400">Invalid evidence type.</response>
    /// <response code="404">The player does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("players/{playerId:guid}/dob-evidence")]
    [ProducesResponseType(typeof(PlayerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerSummaryDto>> DobEvidence(Guid orgId, Guid playerId, [FromBody] DobEvidenceRequest request, CancellationToken ct)
        => Ok(await _registry.RecordDobEvidenceAsync(CallerFor(orgId), PlayerId.FromGuid(playerId), request, ct));

    /// <summary>Proposes a merge of two players (a platform admin executes it).</summary>
    /// <response code="201">The created proposal.</response>
    /// <response code="400">Invalid proposal.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("merge-proposals")]
    [ProducesResponseType(typeof(MergeProposalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MergeProposalDto>> ProposeMerge(Guid orgId, [FromBody] CreateMergeProposalRequest request, CancellationToken ct)
        => Created(await _merge.ProposeAsync(CallerFor(orgId), request, ct));
}
