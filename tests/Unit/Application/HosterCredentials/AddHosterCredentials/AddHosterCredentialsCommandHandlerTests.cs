using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;
using ReplicaGuard.Application.Tests.Testing;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using HosterCredentialsEntity = ReplicaGuard.Core.Domain.Credentials.HosterCredentials;

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
    public async Task Handle_ReturnsFailure_WhenHosterIsMissing()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        AddHosterCredentialsCommand command = CreateApiKeyCommand(hosterId);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns((Hoster?)null);

        // Act
        Result<AddHosterCredentialsResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterErrors.NotFound(hosterId).Code);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCredentialsAlreadyExist()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = HosterTestFactory.CreateWithId(hosterId, "sendcm", Credentials.ApiKey);
        var existingCredentials = hoster.CreateCredentials(userId, apiKey: "existing-key").Value;

        AddHosterCredentialsCommand command = CreateApiKeyCommand(hosterId);

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
    public async Task Handle_ReturnsPendingCredentialsResponse_WhenRequestIsValid()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        Hoster hoster = HosterTestFactory.CreateWithId(hosterId, "sendcm", Credentials.ApiKey);

        AddHosterCredentialsCommand command = CreateApiKeyCommand(hosterId);

        _hosterRepository.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns(hoster);
        _credentialsRepository.FindByUserAndHosterAsync(userId, hosterId, Arg.Any<CancellationToken>())
            .Returns((HosterCredentialsEntity?)null);

        // Act
        Result<AddHosterCredentialsResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HosterId.Should().Be(hosterId);
        result.Value.Status.Should().Be(CredentialsSyncStatus.Pending.ToString().ToLowerInvariant());
    }

    private static AddHosterCredentialsCommand CreateApiKeyCommand(Guid hosterId)
    {
        return new AddHosterCredentialsCommand(
            hosterId,
            "api-key",
            null,
            null,
            null);
    }
}
