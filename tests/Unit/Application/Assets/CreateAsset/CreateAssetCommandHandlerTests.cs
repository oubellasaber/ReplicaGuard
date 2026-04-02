using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Assets;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Application.Tests.Testing;
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
    public async Task Handle_ReturnsFailure_WhenFileNameIsInvalid()
    {
        // Arrange
        CreateAssetCommand command = CreateCommand(fileName: string.Empty);

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReplicationErrors.FileNameEmpty.Code);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenHosterDoesNotExist()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        CreateAssetCommand command = CreateCommand(hosterIds: [hosterId]);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns((Hoster?)null);

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterErrors.NotFound(hosterId).Code);
    }

    [Fact]
    public async Task Handle_ReturnsCreatedAssetResponse_WhenRequestIsValid()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = HosterTestFactory.CreateWithId(hosterId, "sendcm", Credentials.ApiKey);
        HosterCredentialsEntity credentials = hoster.CreateCredentials(userId, apiKey: "api-key").Value;
        credentials.MarkCredentialAsSynced(credentials.Version);

        CreateAssetCommand command = CreateCommand(hosterIds: [hosterId]);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns(credentials);

        // Act
        Result<CreateAssetResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("file.zip");
        result.Value.ReplicaCount.Should().Be(1);
        result.Value.State.Should().Be("created");
    }

    private static CreateAssetCommand CreateCommand(
        string source = "https://example.com/file.zip",
        string fileName = "file.zip",
        List<Guid>? hosterIds = null)
    {
        return new CreateAssetCommand(
            source,
            fileName,
            hosterIds ?? [Guid.NewGuid()]);
    }
}
