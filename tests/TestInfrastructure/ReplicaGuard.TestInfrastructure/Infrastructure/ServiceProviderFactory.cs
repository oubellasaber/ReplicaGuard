using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Caching;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Authentication;
using ReplicaGuard.Infrastructure.Identity;
using ReplicaGuard.Infrastructure.Persistence;
using ReplicaGuard.Infrastructure.Repositories;
using ReplicaGuard.Infrastructure.Seeding;

namespace ReplicaGuard.TestInfrastructure.Infrastructure;

internal static class ServiceProviderFactory
{
    internal static ServiceProvider Create(
        string connectionString,
        DateTime utcNow,
        int refreshTokenExpirationInDays,
        Guid currentUserId,
        string identityId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();

        JwtAuthOptions jwtOptions = new()
        {
            Key = "this_is_a_test_key_with_enough_length_123456",
            Issuer = "replicaguard-tests",
            Audience = "replicaguard-tests",
            ExpirationInMinutes = 15,
            RefreshTokenExpirationInDays = refreshTokenExpirationInDays,
        };

        services.AddSingleton<IOptions<JwtAuthOptions>>(Options.Create(jwtOptions));

        services.AddDbContext<ApplicationDbContext>(options => options
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
            .UseSnakeCaseNamingConvention());

        services.AddDbContext<AppIdentityDbContext>(options => options
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Identity))
            .UseSnakeCaseNamingConvention());

        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<AppIdentityDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenProvider, TokenProvider>();
        services.AddScoped<IJwtAuthOptionsProvider, JwtAuthOptionsProvider>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IHosterRepository, HosterRepository>();
        services.AddScoped<IHosterCredentialsRepository, HosterCredentialsRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUserContext>(_ => new FixedUserContext(currentUserId, identityId));

        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddScoped<CrossContextUnitOfWork>();
        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<CrossContextUnitOfWork>());
        services.AddScoped<AppSeeder>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        internal FixedDateTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class FixedUserContext : IUserContext
    {
        internal FixedUserContext(Guid userId, string identityId)
        {
            UserId = userId;
            IdentityId = identityId;
        }

        public Guid UserId { get; }

        public string IdentityId { get; }
    }
}
