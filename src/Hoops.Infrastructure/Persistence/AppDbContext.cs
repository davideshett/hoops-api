using System.Reflection;
using Hoops.Modules.Competitions.Domain;
using Hoops.Modules.GameRecording.Domain;
using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.Modules.Identity.Domain;
using Hoops.Modules.Registry.Domain;
using Hoops.Modules.Statistics.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.EntityFrameworkCore;
using CompetitionsUnitOfWork = Hoops.Modules.Competitions.Application.Abstractions.ICompetitionsUnitOfWork;
using GameRecordingUnitOfWork = Hoops.Modules.GameRecording.Application.Abstractions.IGameRecordingUnitOfWork;
using RegistryUnitOfWork = Hoops.Modules.Registry.Application.Abstractions.IRegistryUnitOfWork;
using StatisticsUnitOfWork = Hoops.Modules.Statistics.Application.Abstractions.IStatisticsUnitOfWork;

namespace Hoops.Infrastructure.Persistence;

/// <summary>
/// The single application database context (ADR-005). Applies snake_case naming, the citext
/// extension, strongly-typed id conversions, audit-timestamp stamping, and — critically — a global
/// query filter on every <see cref="ITenantScoped"/> entity so a forgotten <c>.Where()</c> can never
/// leak across tenants. Also serves as the Identity module's unit of work.
/// </summary>
public sealed class AppDbContext : DbContext, IUnitOfWork, CompetitionsUnitOfWork, RegistryUnitOfWork, GameRecordingUnitOfWork, StatisticsUnitOfWork
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

    /// <summary>Seasons.</summary>
    public DbSet<Season> Seasons => Set<Season>();

    /// <summary>Competitions.</summary>
    public DbSet<Competition> Competitions => Set<Competition>();

    /// <summary>Stages.</summary>
    public DbSet<Stage> Stages => Set<Stage>();

    /// <summary>Groups (pools).</summary>
    public DbSet<Group> Groups => Set<Group>();

    /// <summary>Canonical teams.</summary>
    public DbSet<Team> Teams => Set<Team>();

    /// <summary>Competition entries.</summary>
    public DbSet<CompetitionTeam> CompetitionTeams => Set<CompetitionTeam>();

    /// <summary>Team staff.</summary>
    public DbSet<TeamStaff> TeamStaff => Set<TeamStaff>();

    /// <summary>Venues.</summary>
    public DbSet<Venue> Venues => Set<Venue>();

    /// <summary>Players — platform-level registry (ADR-003).</summary>
    public DbSet<Player> Players => Set<Player>();

    /// <summary>Player-organisation provenance links.</summary>
    public DbSet<PlayerOrgLink> PlayerOrgLinks => Set<PlayerOrgLink>();

    /// <summary>Consent records.</summary>
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    /// <summary>Player eligibility flags.</summary>
    public DbSet<PlayerEligibilityFlag> EligibilityFlags => Set<PlayerEligibilityFlag>();

    /// <summary>Registry audit rows.</summary>
    public DbSet<RegistryAudit> RegistryAudits => Set<RegistryAudit>();

    /// <summary>The hash-chained provenance ledger.</summary>
    public DbSet<RegistryLedgerEntry> RegistryLedger => Set<RegistryLedgerEntry>();

    /// <summary>Merge proposals.</summary>
    public DbSet<MergeProposal> MergeProposals => Set<MergeProposal>();

    /// <summary>Roster entries — tenant-scoped, built from a registry playerId.</summary>
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();

    /// <summary>Games (fixtures).</summary>
    public DbSet<Game> Games => Set<Game>();

    /// <summary>Frozen game-roster snapshots.</summary>
    public DbSet<GameRosterEntry> GameRosterEntries => Set<GameRosterEntry>();

    /// <summary>Game officials.</summary>
    public DbSet<GameOfficial> GameOfficials => Set<GameOfficial>();

    /// <summary>The append-only game event log — the source of truth (ADR-001).</summary>
    public DbSet<GameEvent> GameEvents => Set<GameEvent>();

    /// <summary>Persisted player statlines (derived).</summary>
    public DbSet<PlayerGameStatline> PlayerGameStatlines => Set<PlayerGameStatline>();

    /// <summary>Persisted team statlines (derived).</summary>
    public DbSet<TeamGameStatline> TeamGameStatlines => Set<TeamGameStatline>();

    /// <summary>Persisted per-period states (derived).</summary>
    public DbSet<GamePeriodStateRow> GamePeriodStates => Set<GamePeriodStateRow>();

    /// <summary>Persisted lineup stints (derived).</summary>
    public DbSet<LineupStintRow> LineupStints => Set<LineupStintRow>();

    /// <summary>Per-competition player aggregates (derived).</summary>
    public DbSet<CompetitionPlayerAggregate> CompetitionPlayerAggregates => Set<CompetitionPlayerAggregate>();

    /// <summary>Cross-organisation career aggregates (derived).</summary>
    public DbSet<PlayerCareerAggregate> PlayerCareerAggregates => Set<PlayerCareerAggregate>();

    /// <summary>Competition standings (derived).</summary>
    public DbSet<CompetitionStanding> CompetitionStandings => Set<CompetitionStanding>();

    /// <summary>The transactional outbox.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// The organisation in scope for the current request, used by tenant query filters. Falls back to
    /// an empty id on non-tenant-scoped requests, which matches no row.
    /// </summary>
    public OrganisationId CurrentOrganisationId => OrganisationId.FromGuid(_tenant.OrganisationId ?? Guid.Empty);

    /// <summary>
    /// Entities that are deliberately NOT tenant-scoped and therefore carry no query filter. This is
    /// the single source of truth the architecture test checks: any mapped entity that is neither
    /// <see cref="ITenantScoped"/> nor listed here fails the build. In Phase 1 the list is the
    /// platform/identity entities; the seven registry entities (ADR-003) are added in Phase 2B.
    /// Never loosen the test — add the intentional exception here, with justification.
    /// </summary>
    public static readonly IReadOnlySet<Type> UnscopedEntityWhitelist = new HashSet<Type>
    {
        // ── Identity / platform infrastructure ──────────────────────────────
        typeof(User),                   // platform-level login; belongs to many orgs
        typeof(Organisation),           // the tenant root itself; has an id, not an organisation_id
        typeof(OrganisationMembership), // must be queryable across orgs (login org list)
        typeof(RefreshToken),           // user-scoped, not org-scoped

        // ── Registry (ADR-003): the national record crosses tenants by design ─
        typeof(Player),                 // the canonical human record, shared by every org
        typeof(PlayerOrgLink),          // read across orgs for provenance
        typeof(ConsentRecord),          // attaches to a player, not an org
        typeof(PlayerEligibilityFlag),  // a platform-scope flag binds every league
        typeof(RegistryAudit),          // registry-wide audit trail
        typeof(RegistryLedgerEntry),    // registry-wide tamper-evident ledger
        typeof(MergeProposal),          // platform-admin queue across all orgs
        typeof(PlayerCareerAggregate),  // a career spans organisations, exactly as identity does (§13)

        // ── Infrastructure ──────────────────────────────────────────────────
        typeof(OutboxMessage),          // durable work queue, not tenant data
    };

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        foreach (var idType in StronglyTypedIdRegistry.All)
        {
            var converterType = typeof(StronglyTypedIdValueConverter<>).MakeGenericType(idType);
            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }

        // Every timestamp is stored in UTC (CLAUDE.md). Callers send whatever offset their clock has.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
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
