using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Identity.Contracts;

/// <summary>
/// Organisation and membership use cases. Authorisation (membership, role) is enforced at the API
/// boundary via policies; these methods assume the caller is already permitted, except where a
/// membership check is intrinsic to the operation.
/// </summary>
public interface IOrganisationService
{
    /// <summary>Lists every organisation the given user belongs to.</summary>
    Task<Result<IReadOnlyList<OrganisationMembershipSummary>>> ListForUserAsync(
        UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Creates an organisation, making <paramref name="creatorUserId"/> its Owner, and returns a fresh
    /// access token carrying that membership so no re-login is needed.
    /// </summary>
    Task<Result<CreateOrganisationResponse>> CreateAsync(
        UserId creatorUserId, CreateOrganisationRequest request, CancellationToken ct = default);

    /// <summary>Fetches one organisation.</summary>
    Task<Result<OrganisationDto>> GetAsync(OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Updates an organisation's mutable profile fields.</summary>
    Task<Result<OrganisationDto>> UpdateAsync(
        OrganisationId organisationId, UpdateOrganisationRequest request, CancellationToken ct = default);

    /// <summary>Lists the members of an organisation.</summary>
    Task<Result<IReadOnlyList<MemberDto>>> ListMembersAsync(
        OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Adds an existing user to an organisation by email.</summary>
    Task<Result<MemberDto>> InviteMemberAsync(
        OrganisationId organisationId, InviteMemberRequest request, CancellationToken ct = default);

    /// <summary>Changes a member's role.</summary>
    Task<Result<MemberDto>> ChangeMemberRoleAsync(
        OrganisationId organisationId, UserId memberUserId, ChangeRoleRequest request, CancellationToken ct = default);

    /// <summary>Removes a member from an organisation.</summary>
    Task<Result> RemoveMemberAsync(
        OrganisationId organisationId, UserId memberUserId, CancellationToken ct = default);
}
