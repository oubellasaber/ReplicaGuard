using MassTransit;
using NSubstitute;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Tests.Messaging.Consumers.Helpers;

internal static class TestData
{
    public static readonly Guid UserId = Guid.NewGuid();
    public static readonly Guid PixeldrainHosterId = Guid.NewGuid();
    public static readonly Guid SendCmHosterId = Guid.NewGuid();

    public static Asset CreateRemoteAsset(params Guid[] hosterIds)
    {
        FileName fileName = FileName.Create("test-file.zip").Value;
        RemoteFileSource source = RemoteFileSource.Create("https://example.com/test-file.zip").Value;
        Asset asset = Asset.CreateFromRemoteUrl(UserId, source, fileName).Value;

        foreach (Guid hosterId in hosterIds)
            asset.AddReplica(hosterId);

        // Clear domain events from creation
        asset.ClearDomainEvents();
        foreach (Replica r in asset.Replicas)
            r.ClearDomainEvents();

        return asset;
    }

    public static Asset CreateLocalAsset(string filePath, params Guid[] hosterIds)
    {
        FileName fileName = FileName.Create("test-file.zip").Value;
        LocalFileSource source = LocalFileSource.Create(filePath).Value;
        Asset asset = Asset.CreateFromLocalPath(UserId, source, fileName).Value;

        foreach (Guid hosterId in hosterIds)
            asset.AddReplica(hosterId);

        asset.ClearDomainEvents();
        foreach (Replica r in asset.Replicas)
            r.ClearDomainEvents();

        return asset;
    }

    public static Hoster CreateHoster(Guid id, string code)
    {
        Hoster hoster = Hoster.Create(code, code, Credentials.ApiKey).Value;
        // Use reflection to set the ID since it's init-only
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(hoster, id);
        return hoster;
    }

    public static HosterCredentials CreateCredentials(Guid userId, Guid hosterId)
    {
        Hoster hoster = CreateHoster(hosterId, "test");
        return hoster.CreateCredentials(userId, apiKey: "test-api-key").Value;
    }

    public static UploadResponse SuccessResponse(long sizeBytes = 1024) =>
        new("file-123", new Uri("https://hoster.com/file/123"), "test-file.zip", sizeBytes, DateTime.UtcNow);

    public static Error MethodNotSupportedError =>
        new("Hoster.Upload.MethodNotSupported", "Not supported");

    public static Error UploadFailedError =>
        new("Hoster.Upload.Failed", "Upload failed");

    public static ConsumeContext<T> MockConsumeContext<T>(T message) where T : class
    {
        ConsumeContext<T> context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }
}
