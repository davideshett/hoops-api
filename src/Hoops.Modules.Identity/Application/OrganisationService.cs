using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.Modules.Identity.Contracts;
using Hoops.Modules.Identity.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Identity.Application;

/// <summary>Orchestrates organisation creation and membership management.</summary>
public sealed class OrganisationService : IOrganisationService
{
    private readonly IOrganisationRepository _organisations;
    private readonly IMembershipRepository _memberships;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public OrganisationService(
        IOrganisationRepository organisations,
        IMembershipRepository memberships,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _organisations = organisations;
        _memberships = memberships;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<OrganisationMembershipSummary>>> ListForUserAsync(
        UserId userId, CancellationToken ct = default)
    {
        var memberships = await _memberships.ListForUserAsync(userId, ct);
        IReadOnlyList<OrganisationMembershipSummary> summaries = memberships
            .Select(m => new OrganisationMembershipSummary(
                m.Organisation.Id, m.Organisation.Name, m.Organisation.Slug, m.Membership.Role.ToString()))
            .ToList();
        return Result.Success(summaries);
    }

    /// <inheritdoc />
    public async Task<Result<OrganisationDto>> CreateAsync(
        UserId creatorUserId, CreateOrganisationRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _organisations.ExistsBySlugAsync(slug, ct))
        {
            return Error.Conflict("SLUG_ALREADY_TAKEN", "An organisation with this slug already exists.");
        }

        var organisation = Organisation.Create(request.Name, slug, request.CountryCode, request.DefaultTimezone);
        _organisations.Add(organisation);

        var membership = OrganisationMembership.CreateActive(
            organisation.Id, creatorUserId, OrganisationRole.Owner, _clock.UtcNow);
        _memberships.Add(membership);

        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(organisation);
    }

    /// <inheritdoc />
    public async Task<Result<OrganisationDto>> GetAsync(OrganisationId organisationId, CancellationToken ct = default)
    {
        var organisation = await _organisations.GetByIdAsync(organisationId, ct);
        return organisation is null
            ? Error.NotFound("ORGANISATION_NOT_FOUND", "The organisation does not exist.")
            : ToDto(organisation);
    }

    /// <inheritdoc />
    public async Task<Result<OrganisationDto>> UpdateAsync(
        OrganisationId organisationId, UpdateOrganisationRequest request, CancellationToken ct = default)
    {
        var organisation = await _organisations.GetByIdAsync(organisationId, ct);
        if (organisation is null)
        {
            return Error.NotFound("ORGANISATION_NOT_FOUND", "The organisation does not exist.");
        }

        organisation.UpdateProfile(request.Name, request.CountryCode, request.DefaultTimezone, request.LogoUrl);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(organisation);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MemberDto>>> ListMembersAsync(
        OrganisationId organisationId, CancellationToken ct = default)
    {
        var members = await _memberships.ListForOrganisationAsync(organisationId, ct);
        IReadOnlyList<MemberDto> dtos = members.Select(ToMemberDto).ToList();
        return Result.Success(dtos);
    }

    /// <inheritdoc />
    public async Task<Result<MemberDto>> InviteMemberAsync(
        OrganisationId organisationId, InviteMemberRequest request, CancellationToken ct = default)
    {
        if (!TryParseRole(request.Role, out var role))
        {
            return Error.Validation("INVALID_ROLE", $"'{request.Role}' is not a valid role.");
        }

        var user = await _users.GetByEmailAsync(request.Email.Trim(), ct);
        if (user is null)
        {
            return Error.NotFound("USER_NOT_FOUND", "No platform user exists with that email.");
        }

        if (await _memberships.GetAsync(organisationId, user.Id, ct) is not null)
        {
            return Error.Conflict("ALREADY_A_MEMBER", "That user is already a member of this organisation.");
        }

        var membership = OrganisationMembership.CreateActive(organisationId, user.Id, role, _clock.UtcNow);
        _memberships.Add(membership);
        await _unitOfWork.SaveChangesAsync(ct);

        return new MemberDto(user.Id, user.Email, user.FullName, role.ToString());
    }

    /// <inheritdoc />
    public async Task<Result<MemberDto>> ChangeMemberRoleAsync(
        OrganisationId organisationId, UserId memberUserId, ChangeRoleRequest request, CancellationToken ct = default)
    {
        if (!TryParseRole(request.Role, out var role))
        {
            return Error.Validation("INVALID_ROLE", $"'{request.Role}' is not a valid role.");
        }

        var membership = await _memberships.GetAsync(organisationId, memberUserId, ct);
        if (membership is null)
        {
            return Error.NotFound("MEMBERSHIP_NOT_FOUND", "That user is not a member of this organisation.");
        }

        if (membership.Role == OrganisationRole.Owner && role != OrganisationRole.Owner
            && await IsLastOwnerAsync(organisationId, memberUserId, ct))
        {
            return Error.Conflict("CANNOT_DEMOTE_LAST_OWNER", "An organisation must retain at least one owner.");
        }

        membership.ChangeRole(role);
        await _unitOfWork.SaveChangesAsync(ct);

        var user = await _users.GetByIdAsync(memberUserId, ct);
        return new MemberDto(memberUserId, user?.Email ?? string.Empty, user?.FullName ?? string.Empty, role.ToString());
    }

    /// <inheritdoc />
    public async Task<Result> RemoveMemberAsync(
        OrganisationId organisationId, UserId memberUserId, CancellationToken ct = default)
    {
        var membership = await _memberships.GetAsync(organisationId, memberUserId, ct);
        if (membership is null)
        {
            return Error.NotFound("MEMBERSHIP_NOT_FOUND", "That user is not a member of this organisation.");
        }

        if (membership.Role == OrganisationRole.Owner && await IsLastOwnerAsync(organisationId, memberUserId, ct))
        {
            return Error.Conflict("CANNOT_REMOVE_LAST_OWNER", "An organisation must retain at least one owner.");
        }

        _memberships.Remove(membership);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<bool> IsLastOwnerAsync(OrganisationId organisationId, UserId candidate, CancellationToken ct)
    {
        var members = await _memberships.ListForOrganisationAsync(organisationId, ct);
        return members.Count(m => m.Membership.Role == OrganisationRole.Owner
            && m.Membership.UserId != candidate) == 0;
    }

    private static bool TryParseRole(string value, out OrganisationRole role)
        => Enum.TryParse(value, ignoreCase: true, out role) && Enum.IsDefined(role);

    private static OrganisationDto ToDto(Organisation o)
        => new(o.Id, o.Name, o.Slug, o.CountryCode, o.DefaultTimezone, o.CreatedAt);

    private static MemberDto ToMemberDto((OrganisationMembership Membership, User User) pair)
        => new(pair.User.Id, pair.User.Email, pair.User.FullName, pair.Membership.Role.ToString());
}
