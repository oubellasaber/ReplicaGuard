using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Assets;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;
using HosterCredentialsEntity = ReplicaGuard.Core.Domain.Credentials.HosterCredentials;

namespace ReplicaGuard.Application.Tests.Assets.CreateAsset;

public class CreateAssetCommandHandlerTests
{
    private readonly IAssetRepository _assetRepository;
    private readonly IHosterRepository _hosterRepository;
    private readonly IHosterCredentialsRepository _credentialsRepository;
    private readonly IUserContext _userContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateAssetCommandHandler _sut;

    public CreateAssetCommandHandlerTests()
    {
        _assetRepository = Substitute.For<IAssetRepository>();
        _hosterRepository = Substitute.For<IHosterRepository>();
        _credentialsRepository = Substitute.For<IHosterCredentialsRepository>();
        _userContext = Substitute.For<IUserContext>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new CreateAssetCommandHandler(
            _assetRepository,
            _hosterRepository,
            _credentialsRepository,
            _userContext,
            _unitOfWork,
            Substitute.For<ILogger<CreateAssetCommandHandler>>());
    }

    [Fact]
    public async Task Handle_InvalidFileName_ReturnsFailure()
    {
        // Arrange
        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            string.Empty,
            new List<Guid> { Guid.NewGuid() });

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReplicationErrors.FileNameEmpty.Code);
    }

    [Fact]
    public async Task Handle_HosterNotFound_ReturnsFailure()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid> { hosterId });

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns((Hoster?)null);

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterErrors.NotFound(hosterId).Code);
    }

    [Fact]
    public async Task Handle_MissingCredentials_ReturnsFailure()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = CreateHoster(hosterId, "sendcm");

        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid> { hosterId });

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns((HosterCredentialsEntity?)null);

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AssetErrors.MissingCredentials(hosterId).Code);
    }

    [Fact]
    public async Task Handle_CredentialsNotSynced_ReturnsFailure()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = CreateHoster(hosterId, "sendcm");
        HosterCredentialsEntity creds = hoster.CreateCredentials(userId, apiKey: "api-key").Value;

        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid> { hosterId });

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns(creds);

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AssetErrors.CredentialsNotSynced(hosterId).Code);
    }

    [Fact]
    public async Task Handle_ValidRequestWithSyncedCredentials_CreatesAssetAndPersists()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = CreateHoster(hosterId, "sendcm");
        HosterCredentialsEntity creds = hoster.CreateCredentials(userId, apiKey: "api-key").Value;
        creds.MarkCredentialAsSynced(creds.Version);

        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid> { hosterId });

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns(creds);

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("file.zip");
        result.Value.ReplicaCount.Should().Be(1);
        result.Value.State.Should().Be("created");

        _assetRepository.Received(1).Add(Arg.Is<Asset>(a =>
            a.UserId == userId &&
            a.FileName.Value == "file.zip" &&
            a.Replicas.Count == 1));

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Hoster CreateHoster(Guid id, string code)
    {
        Hoster hoster = Hoster.Create(code, code, Credentials.ApiKey).Value;
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(hoster, id);
        return hoster;
    }
}
