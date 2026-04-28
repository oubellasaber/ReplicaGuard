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
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Core.Domain.Replication.Planner;
using ReplicaGuard.Core.Domain.Replication.Policies;
using ReplicaGuard.Infrastructure.Authentication;
using ReplicaGuard.Infrastructure.Clock;
using ReplicaGuard.Infrastructure.Data;
using ReplicaGuard.Infrastructure.Hosters.Abstractions;
using ReplicaGuard.Infrastructure.Hosters.Pixeldrain;
using ReplicaGuard.Infrastructure.Hosters.SendCm;
using ReplicaGuard.Infrastructure.Identity;
using ReplicaGuard.Infrastructure.Messaging;
using ReplicaGuard.Infrastructure.Persistence;
using ReplicaGuard.Infrastructure.Policies;
using ReplicaGuard.Infrastructure.Repositories;
using ReplicaGuard.Infrastructure.Seeding;
using ReplicaGuard.Infrastructure.Spool;

namespace ReplicaGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();

        services.AddSingleton<IUploadPlanner, UploadPlanner>();
        services.AddSingleton<IRetryPolicy, ExponentialJitterRetryPolicy>();

        services.AddScoped<ISpoolLeaseService, SqlSpoolLeaseService>();

        AddPersistence(services, configuration);

        //AddCaching(services, configuration);

        AddAuthentication(services, configuration);

        //AddApiVersioning(services);

        AddHttpClients(services, configuration);

        AddApplicationServices(services, configuration);

        AddInfrastructureServices(services, configuration);

        services.AddScoped<AppSeeder>();

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
        services.AddScoped<IHosterCredentialsRepository, HosterCredentialsRepository>();
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
        services.Configure<PixeldrainOptions>(configuration.GetSection("Hosters:Pixeldrain"));
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

        services.AddHttpClient(Pixeldrain.Code, (sp, client) =>
        {
            var cfg = sp.GetRequiredService<IOptions<PixeldrainOptions>>().Value;
            var baseUrl = cfg.ApiBaseUrl ?? throw new InvalidOperationException("Pixeldrain API base URL is not configured.");

            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.AddHttpClient(SendCm.Code, client =>
        {
            client.BaseAddress = new Uri("https://send.cm");
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
    }

    private static void AddInfrastructureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PixeldrainOptions>(configuration.GetSection("Hosters:Pixeldrain"));
        services.Configure<SendcmOptions>(configuration.GetSection("Hosters:SendCm"));

        services.AddScoped<IHosterApiClient, PixeldrainApiClient>();
        services.AddScoped<IHosterApiClient, SendCmApiClient>();

        services.AddScoped<IHosterClientRegistry, HosterClientRegistry>();
    }


    private static void AddApplicationServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IJwtAuthOptionsProvider, JwtAuthOptionsProvider>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenProvider, TokenProvider>();
    }
}
