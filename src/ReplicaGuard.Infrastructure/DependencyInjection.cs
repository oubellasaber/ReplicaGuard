using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Abstractions.Storage;
using ReplicaGuard.Application.Assets.Services;
using ReplicaGuard.Application.Replication.ProgressStreaming;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Domain.Replication.DomainEvents;
using ReplicaGuard.Infrastructure.Authentication;
using ReplicaGuard.Infrastructure.Cleanup;
using ReplicaGuard.Infrastructure.Clock;
using ReplicaGuard.Infrastructure.Data;
using ReplicaGuard.Infrastructure.Encryption;
using ReplicaGuard.Infrastructure.Filtering;
using ReplicaGuard.Infrastructure.Hosters;
using ReplicaGuard.Infrastructure.Hosters.Abstractions;
using ReplicaGuard.Infrastructure.Hosters.Pixeldrain;
using ReplicaGuard.Infrastructure.Hosters.Pixeldrain.IdentityVerification;
using ReplicaGuard.Infrastructure.Hosters.SendCm;
using ReplicaGuard.Infrastructure.Identity;
using ReplicaGuard.Infrastructure.Messaging;
using ReplicaGuard.Infrastructure.Outbox;
using ReplicaGuard.Infrastructure.Persistence;
using ReplicaGuard.Infrastructure.Recovery;
using ReplicaGuard.Infrastructure.Repositories;
using ReplicaGuard.Infrastructure.Seeding;
using ReplicaGuard.Infrastructure.Spool;
using ReplicaGuard.Infrastructure.Storage;
using ReplicaGuard.Infrastructure.Streaming;
using Sieve.Models;
using Sieve.Services;

namespace ReplicaGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();

        AddPersistence(services, configuration);

        services.Configure<FileFetcherOptions>(configuration.GetSection(FileFetcherOptions.SectionName));
        services.AddSingleton<ISpoolFileLocator, DiskSpoolFileLocator>();
        services.AddScoped<ISpoolLeaseService, SqlSpoolLeaseService>();
        services.AddScoped<IFileFetcher, FileFetcher>();
        services.AddSingleton<IReplicaEventStream, SseReplicaEventStream>();
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IStorageMonitor, DiskSpaceMonitor>();
        services.AddScoped<IAssetCleanupService, AssetCleanupService>();
        services.AddHostedService<AssetCleanupBackgroundService>();
        services.AddSingleton<IHosterExpiryPolicy, PixeldrainExpiryPolicy>();
        services.AddSingleton<IHosterExpiryPolicy, SendCmExpiryPolicy>();
        services.AddSingleton<IReplicaExpiryPredictionService, ReplicaExpiryPredictionService>();
        services.Configure<ExpirationRefreshOptions>(configuration.GetSection(ExpirationRefreshOptions.SectionName));
        services.AddScoped<IReplicaRecoveryService, ReplicaRecoveryService>();
        services.AddHostedService<ExpirationRefreshWorker>();
        services.Configure<UserUploadsOptions>(configuration.GetSection(UserUploadsOptions.SectionName));

        //AddCaching(services, configuration);

        AddAuthentication(services, configuration);

        //AddApiVersioning(services);

        AddHttpClients(services, configuration);

        AddApplicationServices(services, configuration);

        AddInfrastructureServices(services, configuration);

        services.AddScoped<AppSeeder>();

        AddHealthChecks(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Database") ??
                                  throw new ArgumentNullException(nameof(configuration));

        services.AddScoped<PublishDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<PublishDomainEventsInterceptor>()));

        services.AddDbContext<AppIdentityDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Identity))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<PublishDomainEventsInterceptor>()));

        services.AddMessaging(configuration);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IHosterRepository, HosterRepository>();
        services.AddScoped<IHosterAccountRepository, HosterAccountRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IReplicaRepository, ReplicaRepository>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<AppIdentityDbContext>();
        services.AddScoped<CrossContextUnitOfWork>();
        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<CrossContextUnitOfWork>());

        services.AddSingleton<ISqlConnectionFactory>(_ =>
            new SqlConnectionFactory(connectionString));

        //SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    public static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<AppIdentityDbContext>();

        services.Configure<JwtAuthOptions>(configuration.GetSection("Jwt"));

        JwtAuthOptions jwtAuthOptions = configuration.GetSection("Jwt").Get<JwtAuthOptions>()!;

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new()
            {
                ValidIssuer = jwtAuthOptions.Issuer,
                ValidAudience = jwtAuthOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtAuthOptions.Key)),
                ValidateIssuerSigningKey = true,
                NameClaimType = JwtRegisteredClaimNames.Email,
                RoleClaimType = JwtCustomClaimNames.Role,
            };
        });

        services.AddAuthorization();

        services.AddScoped<IUserContext, UserContext>();
    }

    private static void AddHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PixeldrainOptions>(configuration.GetSection(PixeldrainOptions.SectionName));
        services.Configure<SendCmOptions>(configuration.GetSection(SendCmOptions.SectionName));
        var userAgent = configuration.GetValue<string>("Hosters:DefaultUserAgent");

        services.AddHttpClient("") // unnamed/default client
        .ConfigureHttpClient(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            return new HttpClientHandler
            {
                AllowAutoRedirect = false,
            };
        });

        services.AddHttpClient("FileUploadingHttpClient")
        .ConfigureHttpClient(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            client.Timeout = TimeSpan.FromHours(1);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            return new HttpClientHandler
            {
                AllowAutoRedirect = false,
            };
        });

        //services.AddHttpClient(Krakenfiles.Code, client =>
        //{
        //    client.BaseAddress = new Uri("https://krakenfiles.com/");
        //    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        //});

        services.AddHttpClient(HosterCode.Pixeldrain.ToFriendlyString(), (sp, client) =>
        {
            var cfg = sp.GetRequiredService<IOptions<PixeldrainOptions>>().Value;
            var baseUrl = cfg.ApiBaseUrl ?? throw new InvalidOperationException("Pixeldrain API base URL is not configured.");

            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.AddHttpClient(HosterCode.SendCm.ToFriendlyString(), client =>
        {
            client.BaseAddress = new Uri("https://send.cm");
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
    }

    private static void AddInfrastructureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IHosterDefinitionResolver, HosterDefinitionResolver>();
        services.AddScoped<IIntegrationEventOutbox, MassTransitIntegrationEventOutbox>();
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.AddTransient<ISecretEncryptionService, AesGcmSecretEncryptionService>();
        services.AddTransient<SendCmUploadSessionProvider>();
        services.AddHosterCapabilitiesFromAssemblies(typeof(DependencyInjection).Assembly);
        services.AddTransient<IIdentityVerificationHandler, PixeldrainIdentityVerificationHandler>();
        //services.AddScoped<IIdentityVerificationHandler, SendCmIdentityVerificationHandler>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<AssetCreatedDomainEventHandler>();
        });
        services.AddScoped<IApplicationDbContext>(sp =>
        sp.GetRequiredService<ApplicationDbContext>());

        // Filtering using Sieve
        // 1. Bind Sieve Options from appsettings.json
        services.Configure<SieveOptions>(configuration.GetSection("Sieve"));
        // 2. Register Custom SieveProcessor
        services.AddScoped<ISieveProcessor, ApplicationSieveProcessor>();
        // 3. Register Generic Grid Query Executor
        services.AddScoped<IGridQueryExecutor, GridQueryExecutor>();
    }

    private static void AddApplicationServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IJwtAuthOptionsProvider, JwtAuthOptionsProvider>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenProvider, TokenProvider>();
    }

    private static void AddHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Database") ??
                                  throw new ArgumentNullException(nameof(configuration));

        services.AddHealthChecks()
            .AddNpgSql(connectionString)
            .AddDbContextCheck<ApplicationDbContext>()
            .AddDbContextCheck<AppIdentityDbContext>();
    }
}
