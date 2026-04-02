using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Infrastructure.Hosters;
using ReplicaGuard.Infrastructure.Messaging.Consumers;
using ReplicaGuard.Infrastructure.Persistence;
using static MassTransit.Logging.OperationName;

namespace ReplicaGuard.Infrastructure.Messaging;

public static class MessagingConfiguration
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")!;

        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        services.Configure<SpoolOptions>(configuration.GetSection("Upload:Spool"));
        services.AddScoped<FileFetcher>();

        MessagingOptions messagingOptions = configuration
            .GetSection(MessagingOptions.SectionName)
            .Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddMassTransit(x =>
        {
            // Credential consumers
            x.AddConsumer<HosterCredentialsCreatedConsumer, HosterCredentialsCreatedConsumerDefinition>();
            x.AddConsumer<HosterCredentialsOutOfSyncConsumer, HosterCredentialsOutOfSyncConsumerDefinition>();
            x.AddConsumer<HosterCredentialsFaultConsumer>();

            // Replication consumers
            x.AddConsumer<AssetCreatedConsumer>();
            x.AddConsumer<UploadReplicaConsumer, UploadReplicaConsumerDefinition>();
            x.AddConsumer<ReplicaCoordinationConsumer>();

            // Configure Entity Framework transactional outbox
            x.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();  // Publish writes to outbox, not directly to transport

                o.QueryDelay = TimeSpan.FromSeconds(messagingOptions.QueryDelayInSeconds);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(messagingOptions.DuplicateDetectionWindowInMinutes);
            });

            // Configure SQL Transport options
            x.AddSqlMessageScheduler();

            x.UsingPostgres((context, cfg) =>
            {
                cfg.UseSqlMessageScheduler();

                cfg.ConfigureEndpoints(context);
            });
        });

        // Configure PostgreSQL transport connection
        services.AddOptions<SqlTransportOptions>()
            .Configure(options =>
            {
                options.ConnectionString = connectionString;
                options.Schema = Schemas.Transport;
            });

        // Run SQL Transport migrations on startup
        services.AddPostgresMigrationHostedService();

        return services;
    }
}
