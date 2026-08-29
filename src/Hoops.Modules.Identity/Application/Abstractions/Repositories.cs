using Hoops.Modules.Identity.Domain;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Application.Abstractions;

/// <summary>Persistence for <see cref="User"/>. Declared here, implemented in Infrastructure.</summary>
public interface IUserRepository
{
    /// <summary>Finds a user by email (case-insensitive), or null.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Finds a user by id, or null.</summary>
    Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default);

    /// <summary>True if a user with this email already exists.</summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Stages a new user for insertion.</summary>
    void Add(User user);
}

/// <summary>Persistence for <see cref="Organisation"/>.</summary>
public interface IOrganisationRepository
{
    /// <summary>Finds an organisation by id, or null.</summary>
    Task<Organisation?> GetByIdAsync(OrganisationId id, CancellationToken ct = default);

    /// <summary>True if an organisation with this slug already exists.</summary>
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Stages a new organisation for insertion.</summary>
    void Add(Organisation organisation);
}

/// <summary>Persistence for <see cref="OrganisationMembership"/>, including cross-organisation reads.</summary>
public interface IMembershipRepository
{
    /// <summary>Finds a specific membership, or null.</summary>
    Task<OrganisationMembership?> GetAsync(OrganisationId organisationId, UserId userId, CancellationToken ct = default);

    /// <summary>Every membership for a user, paired with its organisation (for the login org list).</summary>
    Task<IReadOnlyList<(OrganisationMembership Membership, Organisation Organisation)>> ListForUserAsync(
        UserId userId, CancellationToken ct = default);

    /// <summary>Every membership in an organisation, paired with its user (for the member list).</summary>
    Task<IReadOnlyList<(OrganisationMembership Membership, User User)>> ListForOrganisationAsync(
        OrganisationId organisationId, CancellationToken ct = default);

    /// <summary>Stages a new membership for insertion.</summary>
    void Add(OrganisationMembership membership);

    /// <summary>Stages a membership for deletion.</summary>
    void Remove(OrganisationMembership membership);
}

/// <summary>Persistence for <see cref="RefreshToken"/>.</summary>
public interface IRefreshTokenRepository
{
    /// <summary>Finds a token by its hash, or null.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Stages a new refresh token for insertion.</summary>
    void Add(RefreshToken token);
}

/// <summary>Commits staged changes across the module's repositories in one transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>Persists all staged changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
