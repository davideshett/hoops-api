using System.Reflection;
using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.Modules.Identity.Domain;
using Hoops.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Hoops.Infrastructure.Persistence;

/// <summary>
/// The single application database context (ADR-005). Applies snake_case naming, the citext
/// extension, strongly-typed id conversions, audit-timestamp stamping, and — critically — a global
/// query filter on every <see cref="ITenantScoped"/> entity so a forgotten <c>.Where()</c> can never
/// leak across tenants. Also serves as the Identity module's unit of work.
/// </summary>
public sealed class AppDbContext : DbContext, IUnitOfWork
{
    private static readonly MethodInfo SetTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ICurrentTenant _tenant;
    private readonly IClock _clock;

    /// <summary>Creates the context.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant tenant, IClock clock)
        : base(options)
    {
        _tenant = tenant;
        _clock = clock;
    }

    /// <summary>Platform users.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Organisations (tenants).</summary>
    public DbSet<Organisation> Organisations => Set<Organisation>();

    /// <summary>Organisation memberships.</summary>
    public DbSet<OrganisationMembership> Memberships => Set<OrganisationMembership>();

    /// <summary>Refresh tokens.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// The organisation in scope for the current request, used by tenant query filters. Falls back to
    /// <see cref="Guid.Empty"/> on non-tenant-scoped requests, which matches no row.
    /// </summary>
    public Guid CurrentOrganisationId => _tenant.OrganisationId ?? Guid.Empty;

    /// <summary>
    /// Entities that are deliberately NOT tenant-scoped and therefore carry no query filter. This is
    /// the single source of truth the architecture test checks: any mapped entity that is neither
    /// <see cref="ITenantScoped"/> nor listed here fails the build. In Phase 1 the list is the
    /// platform/identity entities; the seven registry entities (ADR-003) are added in Phase 2B.
    /// Never loosen the test — add the intentional exception here, with justification.
    /// </summary>
    public static readonly IReadOnlySet<Type> UnscopedEntityWhitelist = new HashSet<Type>
    {
        typeof(User),                   // platform-level login; belongs to many orgs
        typeof(Organisation),           // the tenant root itself; has an id, not an organisation_id
        typeof(OrganisationMembership), // must be queryable across orgs (login org list)
        typeof(RefreshToken),           // user-scoped, not org-scoped
    };

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        foreach (var idType in StronglyTypedIdRegistry.All)
        {
            var converterType = typeof(StronglyTypedIdValueConverter<>).MakeGenericType(idType);
            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                SetTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        StampAudits();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudits();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantScoped
        => builder.Entity<TEntity>().HasQueryFilter(e => e.OrganisationId == CurrentOrganisationId);

    private void StampAudits()
    {
        var now = _clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
