using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Hosters;
using ReplicaGuard.Infrastructure.Messaging.Commands;
using ReplicaGuard.Infrastructure.Messaging.Consumers;
using ReplicaGuard.Infrastructure.Tests.Messaging.Consumers.Helpers;

namespace ReplicaGuard.Infrastructure.Tests.Messaging.Consumers;

public class UploadReplicaConsumerTests
{
    private readonly IAssetRepository _assetRepository;
    private readonly IReplicaRepository _replicaRepository;
    private readonly IHosterRepository _hosterRepository;
    private readonly IHosterCredentialsRepository _credentialsRepository;
    private readonly IHosterClientRegistry _hosterRegistry;
    private readonly FileFetcher _fileFetcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUploadFile _uploader;
    private readonly UploadReplicaConsumer _sut;

    public UploadReplicaConsumerTests()
    {
        _assetRepository = Substitute.For<IAssetRepository>();
        _replicaRepository = Substitute.For<IReplicaRepository>();
        _hosterRepository = Substitute.For<IHosterRepository>();
        _credentialsRepository = Substitute.For<IHosterCredentialsRepository>();
        _hosterRegistry = Substitute.For<IHosterClientRegistry>();
        _fileFetcher = Substitute.For<FileFetcher>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _uploader = Substitute.For<IUploadFile>();

        _sut = new UploadReplicaConsumer(
            _assetRepository,
            _replicaRepository,
            _hosterRepository,
            _credentialsRepository,
            _hosterRegistry,
            _fileFetcher,
            _unitOfWork,
            Substitute.For<ILogger<UploadReplicaConsumer>>());
    }

    #region Remote URL Upload (Hoster Fetches Directly)

    [Fact]
    public async Task Consume_RemoteSource_HosterSupportsRemoteUrl_UploadsViaRemoteUrl()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId);
        Replica replica = asset.Replicas[0];
        Hoster hoster = TestData.CreateHoster(TestData.SendCmHosterId, "SENDCM");

        SetupStandardMocks(replica, asset, hoster);

        _uploader.UploadFromRemoteUrlAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(TestData.SuccessResponse()));

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert
        replica.State.Should().Be(ReplicaState.Completed);
        replica.Link.Should().NotBeNull();
        await _unitOfWork.Received(AtLeast(1)).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_RemoteSource_HosterSupportsRemoteUrl_RecordsFileSizeFromResponse()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId);
        Replica replica = asset.Replicas[0];
        Hoster hoster = TestData.CreateHoster(TestData.SendCmHosterId, "SENDCM");

        SetupStandardMocks(replica, asset, hoster);

        _uploader.UploadFromRemoteUrlAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(TestData.SuccessResponse(sizeBytes: 5000)));

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert
        asset.SizeBytes.Should().Be(5000);
    }

    #endregion

    #region Fallback to Local Stream

    [Fact]
    public async Task Consume_RemoteSource_HosterDoesNotSupportRemoteUrl_FallsBackToLocalStream()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.PixeldrainHosterId);
        Replica replica = asset.Replicas[0];
        Hoster hoster = TestData.CreateHoster(TestData.PixeldrainHosterId, "PIXELDRAIN");

        SetupStandardMocks(replica, asset, hoster);

        // Remote URL returns MethodNotSupported
        _uploader.UploadFromRemoteUrlAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UploadResponse>(TestData.MethodNotSupportedError));

        // FileFetcher downloads successfully
        _fileFetcher.IsSpooled(asset.Id).Returns(false);
        _fileFetcher.DownloadAsync(asset.Id, Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new FetchedFile("/spool/file", 2048)));
        _fileFetcher.GetSpoolPath(asset.Id).Returns("/spool/file");

        // Local upload succeeds
        _uploader.UploadFromLocalStroageAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<FileStream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(TestData.SuccessResponse()));

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert
        await _fileFetcher.Received(1).DownloadAsync(
            asset.Id, Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>());
        asset.SizeBytes.Should().Be(2048);
    }

    #endregion

    #region Peer Waiting

    [Fact]
    public async Task Consume_RemoteSource_SiblingUploading_WaitsForPeer()
    {
        // Arrange — two replicas, first one is already uploading
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId, TestData.PixeldrainHosterId);
        Replica sendCmReplica = asset.Replicas[0];
        Replica pixeldrainReplica = asset.Replicas[1];
        Hoster hoster = TestData.CreateHoster(TestData.PixeldrainHosterId, "PIXELDRAIN");

        // SendCM is already uploading
        sendCmReplica.MarkUploading();
        sendCmReplica.ClearDomainEvents();

        SetupStandardMocks(pixeldrainReplica, asset, hoster);

        // Pixeldrain doesn't support remote URL
        _uploader.UploadFromRemoteUrlAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UploadResponse>(TestData.MethodNotSupportedError));

        _fileFetcher.IsSpooled(asset.Id).Returns(false);

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(pixeldrainReplica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert
        pixeldrainReplica.State.Should().Be(ReplicaState.WaitingForPeer);
        pixeldrainReplica.WaitingForReplicaId.Should().Be(sendCmReplica.Id);

        // Should schedule a safety timeout
        await context.Received(1).SchedulePublish(
            Arg.Any<TimeSpan>(),
            Arg.Any<UploadReplicaCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_RemoteSource_SiblingUploading_FileAlreadySpooled_UploadsDirectly()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId, TestData.PixeldrainHosterId);
        Replica sendCmReplica = asset.Replicas[0];
        Replica pixeldrainReplica = asset.Replicas[1];
        Hoster hoster = TestData.CreateHoster(TestData.PixeldrainHosterId, "PIXELDRAIN");

        sendCmReplica.MarkUploading();
        sendCmReplica.ClearDomainEvents();

        SetupStandardMocks(pixeldrainReplica, asset, hoster);

        _uploader.UploadFromRemoteUrlAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UploadResponse>(TestData.MethodNotSupportedError));

        // File is already spooled from sibling's download
        _fileFetcher.IsSpooled(asset.Id).Returns(true);
        _fileFetcher.GetSpoolPath(asset.Id).Returns("/spool/file");
        _fileFetcher.DownloadAsync(asset.Id, Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new FetchedFile("/spool/file", 2048)));

        _uploader.UploadFromLocalStroageAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<FileStream>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(TestData.SuccessResponse()));

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(pixeldrainReplica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert — should NOT wait, should upload directly
        pixeldrainReplica.State.Should().NotBe(ReplicaState.WaitingForPeer);
    }

    #endregion

    #region Failure + Retry

    [Fact]
    public async Task Consume_UploadFails_MarksReplicaFailed()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId);
        Replica replica = asset.Replicas[0];
        Hoster hoster = TestData.CreateHoster(TestData.SendCmHosterId, "SENDCM");

        SetupStandardMocks(replica, asset, hoster);

        _uploader.UploadFromRemoteUrlAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UploadResponse>(TestData.UploadFailedError));

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert
        replica.State.Should().Be(ReplicaState.Failed);
        replica.LastError.Should().Be("Upload failed");
        replica.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task Consume_UploadFails_CanRetry_SchedulesRetry()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId);
        Replica replica = asset.Replicas[0];
        Hoster hoster = TestData.CreateHoster(TestData.SendCmHosterId, "SENDCM");

        SetupStandardMocks(replica, asset, hoster);

        _uploader.UploadFromRemoteUrlAsync(
            Arg.Any<CredentialSet>(), Arg.Any<string>(), Arg.Any<RemoteFileSource>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UploadResponse>(TestData.UploadFailedError));

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert — retry scheduled
        await context.Received(1).SchedulePublish(
            Arg.Any<TimeSpan>(),
            Arg.Any<UploadReplicaCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ReplicaAlreadyCompleted_DoesNothing()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId);
        Replica replica = asset.Replicas[0];
        replica.MarkUploading();
        replica.MarkCompleted(new Uri("https://done.com/file"));
        replica.ClearDomainEvents();

        _replicaRepository.GetByIdAsync(replica.Id, Arg.Any<CancellationToken>())
            .Returns(replica);

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, TestData.SendCmHosterId));

        // Act
        await _sut.Consume(context);

        // Assert
        await _assetRepository.DidNotReceive().GetByIdWithReplicasAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Missing Dependencies

    [Fact]
    public async Task Consume_NoCredentials_MarksFailed()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId);
        Replica replica = asset.Replicas[0];
        Hoster hoster = TestData.CreateHoster(TestData.SendCmHosterId, "SENDCM");

        _replicaRepository.GetByIdAsync(replica.Id, Arg.Any<CancellationToken>())
            .Returns(replica);
        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);
        _hosterRepository.GetByIdAsync(hoster.Id, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _hosterRegistry.TryGetHosterCapability<IUploadFile>(hoster.Code)
            .Returns(_uploader);

        // No credentials
        _credentialsRepository.FindByUserAndHosterAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((HosterCredentials?)null);

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert
        replica.State.Should().Be(ReplicaState.Failed);
        replica.LastError.Should().Contain("No credentials");
    }

    [Fact]
    public async Task Consume_NoUploadCapability_MarksFailed()
    {
        // Arrange
        Asset asset = TestData.CreateRemoteAsset(TestData.SendCmHosterId);
        Replica replica = asset.Replicas[0];
        Hoster hoster = TestData.CreateHoster(TestData.SendCmHosterId, "SENDCM");

        _replicaRepository.GetByIdAsync(replica.Id, Arg.Any<CancellationToken>())
            .Returns(replica);
        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);
        _hosterRepository.GetByIdAsync(hoster.Id, Arg.Any<CancellationToken>())
            .Returns(hoster);

        // No upload capability
        _hosterRegistry.TryGetHosterCapability<IUploadFile>(hoster.Code)
            .Returns((IUploadFile?)null);

        var context = TestData.MockConsumeContext(
            new UploadReplicaCommand(replica.Id, asset.Id, hoster.Id));

        // Act
        await _sut.Consume(context);

        // Assert
        replica.State.Should().Be(ReplicaState.Failed);
        replica.LastError.Should().Contain("does not support uploads");
    }

    #endregion

    #region Helpers

    private void SetupStandardMocks(Replica replica, Asset asset, Hoster hoster)
    {
        _replicaRepository.GetByIdAsync(replica.Id, Arg.Any<CancellationToken>())
            .Returns(replica);
        _assetRepository.GetByIdWithReplicasAsync(asset.Id, Arg.Any<CancellationToken>())
            .Returns(asset);
        _hosterRepository.GetByIdAsync(hoster.Id, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _hosterRegistry.TryGetHosterCapability<IUploadFile>(hoster.Code)
            .Returns(_uploader);

        HosterCredentials creds = TestData.CreateCredentials(TestData.UserId, hoster.Id);
        _credentialsRepository.FindByUserAndHosterAsync(
            Arg.Any<Guid>(), hoster.Id, Arg.Any<CancellationToken>())
            .Returns(creds);
    }

    private static Quantity AtLeast(int count) => Quantity.Within(count, int.MaxValue);

    #endregion
}
