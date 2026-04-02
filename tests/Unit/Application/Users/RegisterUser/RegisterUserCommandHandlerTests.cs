using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Users.RegisterUser;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.User;

namespace ReplicaGuard.Application.Tests.Users.RegisterUser;

public class RegisterUserCommandHandlerTests
{
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;
    private readonly RegisterUserCommandHandler _sut;

    public RegisterUserCommandHandlerTests()
    {
        IUserRepository userRepository = Substitute.For<IUserRepository>();
        _identityService = Substitute.For<IIdentityService>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        IRefreshTokenRepository refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        IIdentityUnitOfWork unitOfWork = Substitute.For<IIdentityUnitOfWork>();

        _sut = new RegisterUserCommandHandler(
            userRepository,
            _identityService,
            _tokenProvider,
            refreshTokenRepository,
            unitOfWork);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenEmailAlreadyExists()
    {
        // Arrange
        RegisterUserCommand command = CreateValidCommand();
        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.EmailAlreadyTaken(string.Empty).Code);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenUsernameAlreadyExists()
    {
        // Arrange
        RegisterUserCommand command = CreateValidCommand();
        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);
        _identityService.UsernameExistsAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.UsernameAlreadyTaken(string.Empty).Code);
    }

    [Fact]
    public async Task Handle_RollsBackAndReturnsFailure_WhenIdentityCreationFails()
    {
        // Arrange
        RegisterUserCommand command = CreateValidCommand();
        Error identityError = new("Identity.Failure", "Password too weak");

        SetupSuccessfulPreChecks(command);
        _identityService.CreateUserAsync(
                command.Name,
                command.Email,
                command.Password,
                Roles.Member,
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IdentityUser>(identityError));

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(identityError);
    }

    [Fact]
    public async Task Handle_CreatesUserAndCommitsTransaction_WhenRequestIsValid()
    {
        // Arrange
        RegisterUserCommand command = CreateValidCommand();
        IdentityUser identityUser = new() { Id = "user-123", Email = command.Email };
        (string AccessToken, string RefreshToken) expectedTokens = ("access-token", "refresh-token");

        SetupSuccessfulRegistration(command, identityUser, expectedTokens);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(expectedTokens.AccessToken);
        result.Value.RefreshToken.Should().Be(expectedTokens.RefreshToken);
    }

    private static RegisterUserCommand CreateValidCommand() =>
        new("JohnDoe", "john@example.com", "SecurePassword123!", "SecurePassword123!");

    private void SetupSuccessfulPreChecks(RegisterUserCommand command)
    {
        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);
        _identityService.UsernameExistsAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);
    }

    private void SetupSuccessfulRegistration(
        RegisterUserCommand command,
        IdentityUser identityUser,
        (string AccessToken, string RefreshToken) tokens)
    {
        SetupSuccessfulPreChecks(command);

        _identityService.CreateUserAsync(
                command.Name,
                command.Email,
                command.Password,
                Roles.Member,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(identityUser));

        _tokenProvider.Create(
                identityUser.Id,
                identityUser.Email!,
                Arg.Any<IEnumerable<string>>())
            .Returns(tokens);
    }
}
