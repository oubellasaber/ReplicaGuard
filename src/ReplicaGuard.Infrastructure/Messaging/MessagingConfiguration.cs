using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Infrastructure.Messaging.Commands;
using ReplicaGuard.Infrastructure.Messaging.Consumers;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Messaging;

public static class MessagingConfiguration
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")!;

        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));

        MessagingOptions messagingOptions = configuration
            .GetSection(MessagingOptions.SectionName)
            .Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddMassTransit(x =>
        {
            // 1. Register ALL consumers + definitions
            x.AddConsumer<UploadReplicaFaultConsumer>();
            x.AddConsumer<UploadReplicaConsumer, UploadReplicaConsumerDefinition>();
            x.AddConsumer<ReplicaTerminalIntegrationEventConsumer, ReplicaTerminalIntegrationEventConsumerDefinition>();
            x.AddConsumer<IdentityCreatedIntegrationEventConsumer, IdentityCreatedIntegrationEventConsumerDefinition>();
            x.AddConsumer<IdentityCreatedIntegrationEventFaultConsumer>();
            x.AddConsumer<AssetCreatedIntegrationEventConsumer, AssetCreatedIntegrationEventConsumerDefinition>();
            x.AddConsumers(typeof(UploadReplicaConsumer).Assembly);
            EndpointConvention.Map<UploadReplicaCommand>(new Uri("queue:upload-replica"));



            // 2. Configure EF Outbox (transactional)
            x.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox(); // publish goes to outbox table

                o.QueryDelay = TimeSpan.FromSeconds(messagingOptions.QueryDelayInSeconds);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(messagingOptions.DuplicateDetectionWindowInMinutes);
            });


            // 3. Configure SQL Transport + Scheduler
            x.AddSqlMessageScheduler();

            x.UsingPostgres((context, cfg) =>
            {
                cfg.UseSqlMessageScheduler();
                cfg.ConfigureEndpoints(context);
            });
        });

        // 4. Configure SQL Transport options
        services.AddOptions<SqlTransportOptions>()
            .Configure(options =>
            {
                options.ConnectionString = connectionString;
                options.Schema = Schemas.Transport;
            });

        // 5. Run SQL Transport migrations on startup
        services.AddPostgresMigrationHostedService();

        return services;
    }
}
