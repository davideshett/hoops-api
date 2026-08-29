using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.Modules.Identity.Domain;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IUserRepository"/>.</summary>
public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => db.Users.AnyAsync(u => u.Email == email, ct);

    /// <inheritdoc />
    public void Add(User user) => db.Users.Add(user);
}

/// <summary>EF Core implementation of <see cref="IOrganisationRepository"/>.</summary>
public sealed class OrganisationRepository(AppDbContext db) : IOrganisationRepository
{
    /// <inheritdoc />
    public Task<Organisation?> GetByIdAsync(OrganisationId id, CancellationToken ct = default)
        => db.Organisations.FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
        => db.Organisations.AnyAsync(o => o.Slug == slug, ct);

    /// <inheritdoc />
    public void Add(Organisation organisation) => db.Organisations.Add(organisation);
}

/// <summary>EF Core implementation of <see cref="IMembershipRepository"/>.</summary>
public sealed class MembershipRepository(AppDbContext db) : IMembershipRepository
{
    /// <inheritdoc />
    public Task<OrganisationMembership?> GetAsync(OrganisationId organisationId, UserId userId, CancellationToken ct = default)
        => db.Memberships.FirstOrDefaultAsync(m => m.OrganisationId == organisationId && m.UserId == userId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<(OrganisationMembership Membership, Organisation Organisation)>> ListForUserAsync(
        UserId userId, CancellationToken ct = default)
    {
        var rows = await db.Memberships
            .Where(m => m.UserId == userId)
            .Join(db.Organisations, m => m.OrganisationId, o => o.Id, (m, o) => new { m, o })
            .OrderBy(x => x.o.Name)
            .ToListAsync(ct);
        return rows.Select(x => (x.m, x.o)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(OrganisationMembership Membership, User User)>> ListForOrganisationAsync(
        OrganisationId organisationId, CancellationToken ct = default)
    {
        var rows = await db.Memberships
            .Where(m => m.OrganisationId == organisationId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m, u })
            .OrderBy(x => x.u.FullName)
            .ToListAsync(ct);
        return rows.Select(x => (x.m, x.u)).ToList();
    }

    /// <inheritdoc />
    public void Add(OrganisationMembership membership) => db.Memberships.Add(membership);

    /// <inheritdoc />
    public void Remove(OrganisationMembership membership) => db.Memberships.Remove(membership);
}

/// <summary>EF Core implementation of <see cref="IRefreshTokenRepository"/>.</summary>
public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    /// <inheritdoc />
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    /// <inheritdoc />
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}
