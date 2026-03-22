using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.Tests.HosterCredentials.AddHosterCredentials;

public class AddHosterCredentialsCommandHandlerTests
{
    private readonly IHosterCredentialsRepository _credentialsRepository;
    private readonly IHosterRepository _hosterRepository;
    private readonly IUserContext _userContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AddHosterCredentialsCommandHandler _sut;

    public AddHosterCredentialsCommandHandlerTests()
    {
        _credentialsRepository = Substitute.For<IHosterCredentialsRepository>();
        _hosterRepository = Substitute.For<IHosterRepository>();
        _userContext = Substitute.For<IUserContext>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new AddHosterCredentialsCommandHandler(
            _credentialsRepository,
            _hosterRepository,
            _userContext,
            _unitOfWork,
            Substitute.For<ILogger<AddHosterCredentialsCommand>>());
    }

    [Fact]
    public async Task Handle_HosterNotFound_ReturnsFailure()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        var command = new AddHosterCredentialsCommand(
            hosterId,
            "api-key",
            null,
            null,
            null);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns((Hoster?)null);

        // Act
        Result<AddHosterCredentialsResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterErrors.NotFound(hosterId).Code);
    }

    [Fact]
    public async Task Handle_ExistingCredentials_ReturnsAlreadyExistsFailure()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = CreateHoster(hosterId, "sendcm", Credentials.ApiKey);
        var existingCredentials = hoster.CreateCredentials(userId, apiKey: "existing-key").Value;

        var command = new AddHosterCredentialsCommand(
            hosterId,
            "api-key",
            null,
            null,
            null);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns(existingCredentials);

        // Act
        Result<AddHosterCredentialsResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterCredentialsErrors.AlreadyExists(hoster.Code).Code);
    }

    [Fact]
    public async Task Handle_InvalidCredentialPayloadForHoster_ReturnsFailure()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = CreateHoster(hosterId, "sendcm", Credentials.ApiKey);

        var command = new AddHosterCredentialsCommand(
            hosterId,
            null,
            null,
            null,
            null);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns((ReplicaGuard.Core.Domain.Credentials.HosterCredentials?)null);

        // Act
        Result<AddHosterCredentialsResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterCredentialsErrors.MissingApiKey().Code);
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsCredentialsAndPersists()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = CreateHoster(hosterId, "sendcm", Credentials.ApiKey);

        var command = new AddHosterCredentialsCommand(
            hosterId,
            "api-key",
            null,
            null,
            null);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns((ReplicaGuard.Core.Domain.Credentials.HosterCredentials?)null);

        // Act
        Result<AddHosterCredentialsResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HosterId.Should().Be(hosterId);
        result.Value.Status.Should().Be(CredentialsSyncStatus.Pending.ToString().ToLowerInvariant());

        _credentialsRepository.Received(1).Add(Arg.Is<ReplicaGuard.Core.Domain.Credentials.HosterCredentials>(c =>
            c.UserId == userId &&
            c.HosterId == hosterId &&
            c.ApiKey == "api-key"));

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Hoster CreateHoster(Guid id, string code, Credentials primaryCredentials)
    {
        Hoster hoster = Hoster.Create(code, code, primaryCredentials).Value;
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(hoster, id);
        return hoster;
    }
}
