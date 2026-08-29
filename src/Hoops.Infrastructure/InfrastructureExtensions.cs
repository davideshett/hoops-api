using Hoops.Infrastructure.Persistence;
using Hoops.Infrastructure.Persistence.Repositories;
using Hoops.Infrastructure.Security;
using Hoops.Infrastructure.Time;
using Hoops.Modules.Identity.Application.Abstractions;
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

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganisationRepository, OrganisationRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
