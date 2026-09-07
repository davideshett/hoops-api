using Hoops.Infrastructure.Persistence;
using Hoops.Infrastructure.Persistence.Repositories;
using Hoops.Infrastructure.Security;
using Hoops.Infrastructure.Time;
using Hoops.Modules.Competitions.Application.Abstractions;
using Hoops.Modules.GameRecording.Application.Abstractions;
using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.Modules.Registry.Application.Abstractions;
using Hoops.Modules.Statistics.Application.Abstractions;
using Hoops.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hoops.Infrastructure;

/// <summary>Registers the database context, repositories, and cross-cutting infrastructure services.</summary>
public static class InfrastructureExtensions
{
    /// <summary>The configuration key holding the Postgres connection string.</summary>
    public const string ConnectionStringName = "Postgres";

    /// <summary>Wires up EF Core (Npgsql, snake_case), repositories, the clock, and the password hasher.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            options.UseSnakeCaseNamingConvention();
        });

        // Identity module persistence.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganisationRepository, OrganisationRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Competitions module persistence.
        services.AddScoped<ICompetitionsUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<ICompetitionRepository, CompetitionRepository>();
        services.AddScoped<IStageRepository, StageRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ICompetitionTeamRepository, CompetitionTeamRepository>();
        services.AddScoped<ITeamStaffRepository, TeamStaffRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();

        // Registry security (ADR-008): NIN pepper from config, delegated verification + photo storage stubs.
        services.Configure<RegistryOptions>(configuration.GetSection(RegistryOptions.SectionName));
        services.AddSingleton<INinHasher, HmacNinHasher>();
        services.AddSingleton<INinVerificationProvider, StubNinVerificationProvider>();
        services.AddSingleton<IPhotoStorage, StubPhotoStorage>();

        // Registry module persistence.
        services.AddScoped<IRegistryUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IPlayerOrgLinkRepository, PlayerOrgLinkRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<IEligibilityFlagRepository, EligibilityFlagRepository>();
        services.AddScoped<IRegistryAuditRepository, RegistryAuditRepository>();
        services.AddScoped<IRegistryLedgerRepository, RegistryLedgerRepository>();
        services.AddScoped<IMergeProposalRepository, MergeProposalRepository>();
        services.AddScoped<IRosterRepository, RosterRepository>();
        services.AddScoped<IPlayerStatisticsRepointer, PlayerStatisticsRepointer>();

        // GameRecording module persistence.
        services.AddScoped<IGameRecordingUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGameRosterRepository, GameRosterRepository>();
        services.AddScoped<IGameEventRepository, GameEventRepository>();
        services.AddScoped<IGameOfficialRepository, GameOfficialRepository>();
        services.AddScoped<IRosterSnapshotSource, RosterSnapshotSource>();
        services.AddScoped<IGameCompetitionSource, GameCompetitionSource>();

        // Statistics module persistence.
        services.AddScoped<IStatisticsUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IGameStatisticsSource, GameStatisticsSource>();
        services.AddScoped<IHistoryReadRepository, HistoryReadRepository>();

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
