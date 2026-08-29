using Hoops.Api.Auth;
using Hoops.Modules.Identity.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Identity;

/// <summary>Organisation profiles and membership management. Org-scoped actions carry an explicit policy.</summary>
[Route("api/v1/[controller]")]
public sealed class OrganisationsController : ApiControllerBase
{
    private readonly IOrganisationService _organisations;
    private readonly ICurrentUser _currentUser;

    /// <summary>Creates the controller.</summary>
    public OrganisationsController(IOrganisationService organisations, ICurrentUser currentUser)
    {
        _organisations = organisations;
        _currentUser = currentUser;
    }

    private UserId CurrentUserId => UserId.FromGuid(_currentUser.UserId!.Value);

    /// <summary>Lists every organisation the caller belongs to.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The caller's organisations, with their role in each.</response>
    /// <response code="401">The caller is not authenticated.</response>
    [Authorize(Policy = AuthPolicies.Authenticated)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrganisationMembershipSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrganisationMembershipSummary>>> List(CancellationToken ct)
        => Ok(await _organisations.ListForUserAsync(CurrentUserId, ct));

    /// <summary>Creates an organisation, making the caller its Owner.</summary>
    /// <param name="request">The organisation to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">The organisation was created.</response>
    /// <response code="400">The request payload was invalid.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="409">The slug is already taken.</response>
    [Authorize(Policy = AuthPolicies.Authenticated)]
    [HttpPost]
    [ProducesResponseType(typeof(OrganisationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganisationDto>> Create(
        [FromBody] CreateOrganisationRequest request, CancellationToken ct)
    {
        var result = await _organisations.CreateAsync(CurrentUserId, request, ct);
        return result.IsSuccess
            ? Created(result, $"/api/v1/organisations/{result.Value.Id.Value}")
            : Created(result);
    }

    /// <summary>Fetches one organisation.</summary>
    /// <param name="orgId">The organisation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The organisation.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="403">The caller is not a member of this organisation.</response>
    /// <response code="404">The organisation does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{orgId:guid}")]
    [ProducesResponseType(typeof(OrganisationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganisationDto>> Get(Guid orgId, CancellationToken ct)
        => Ok(await _organisations.GetAsync(OrganisationId.FromGuid(orgId), ct));

    /// <summary>Updates an organisation's mutable profile fields.</summary>
    /// <param name="orgId">The organisation id.</param>
    /// <param name="request">The fields to change; omitted fields are unchanged.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The updated organisation.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="403">The caller is not an admin of this organisation.</response>
    /// <response code="404">The organisation does not exist.</response>
    [Authorize(Policy = AuthPolicies.OrgAdmin)]
    [HttpPatch("{orgId:guid}")]
    [ProducesResponseType(typeof(OrganisationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganisationDto>> Update(
        Guid orgId, [FromBody] UpdateOrganisationRequest request, CancellationToken ct)
        => Ok(await _organisations.UpdateAsync(OrganisationId.FromGuid(orgId), request, ct));

    /// <summary>Lists the members of an organisation.</summary>
    /// <param name="orgId">The organisation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The members.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="403">The caller is not a member of this organisation.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("{orgId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<MemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> ListMembers(Guid orgId, CancellationToken ct)
        => Ok(await _organisations.ListMembersAsync(OrganisationId.FromGuid(orgId), ct));

    /// <summary>Adds an existing platform user to an organisation by email.</summary>
    /// <param name="orgId">The organisation id.</param>
    /// <param name="request">The user's email and the role to grant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">The member was added.</response>
    /// <response code="400">The role is invalid.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="403">The caller is not an admin of this organisation.</response>
    /// <response code="404">No user exists with that email.</response>
    /// <response code="409">That user is already a member.</response>
    [Authorize(Policy = AuthPolicies.OrgAdmin)]
    [HttpPost("{orgId:guid}/members/invite")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MemberDto>> InviteMember(
        Guid orgId, [FromBody] InviteMemberRequest request, CancellationToken ct)
        => Created(await _organisations.InviteMemberAsync(OrganisationId.FromGuid(orgId), request, ct));

    /// <summary>Changes a member's role.</summary>
    /// <param name="orgId">The organisation id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <param name="request">The new role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The updated member.</response>
    /// <response code="400">The role is invalid.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="403">The caller is not an admin of this organisation.</response>
    /// <response code="404">That user is not a member.</response>
    /// <response code="409">The change would leave the organisation without an owner.</response>
    [Authorize(Policy = AuthPolicies.OrgAdmin)]
    [HttpPatch("{orgId:guid}/members/{userId:guid}")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MemberDto>> ChangeMemberRole(
        Guid orgId, Guid userId, [FromBody] ChangeRoleRequest request, CancellationToken ct)
        => Ok(await _organisations.ChangeMemberRoleAsync(
            OrganisationId.FromGuid(orgId), UserId.FromGuid(userId), request, ct));

    /// <summary>Removes a member from an organisation.</summary>
    /// <param name="orgId">The organisation id.</param>
    /// <param name="userId">The member's user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">The member was removed.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="403">The caller is not an admin of this organisation.</response>
    /// <response code="404">That user is not a member.</response>
    /// <response code="409">The removal would leave the organisation without an owner.</response>
    [Authorize(Policy = AuthPolicies.OrgAdmin)]
    [HttpDelete("{orgId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RemoveMember(Guid orgId, Guid userId, CancellationToken ct)
        => NoContent(await _organisations.RemoveMemberAsync(
            OrganisationId.FromGuid(orgId), UserId.FromGuid(userId), ct));
}
