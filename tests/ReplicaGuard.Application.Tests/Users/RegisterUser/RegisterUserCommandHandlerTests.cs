using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Users.RegisterUser;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.User;

namespace ReplicaGuard.Application.Tests.Users.RegisterUser;

public class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly RegisterUserCommandHandler _sut;

    public RegisterUserCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _identityService = Substitute.For<IIdentityService>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

        _sut = new RegisterUserCommandHandler(
            _userRepository,
            _identityService,
            _tokenProvider,
            _refreshTokenRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var command = CreateValidCommand();
        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.EmailAlreadyTaken(string.Empty).Code);
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_DoesNotBeginTransaction()
    {
        // Arrange
        var command = CreateValidCommand();
        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.DidNotReceive()
            .BeginTransactionAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UsernameAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var command = CreateValidCommand();
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
    public async Task Handle_IdentityCreationFails_RollsBackAndReturnsFailure()
    {
        // Arrange
        var command = CreateValidCommand();
        var identityError = new Error("Identity.Failure", "Password too weak");

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
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_CreatesUserAndReturnsTokens()
    {
        // Arrange
        var command = CreateValidCommand();
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        SetupSuccessfulRegistration(command, identityUser, expectedTokens);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(expectedTokens.Item1);
        result.Value.RefreshToken.Should().Be(expectedTokens.Item2);
        _userRepository.Received(1).Add(Arg.Is<User>(u =>
            u.Email == command.Email && u.Name == command.Name));
    }

    [Fact]
    public async Task Handle_Success_AddsRefreshToken()
    {
        // Arrange
        var command = CreateValidCommand();
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        SetupSuccessfulRegistration(command, identityUser, expectedTokens);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepository.Received(1).Add(expectedTokens.Item2, identityUser.Id);
    }

    [Fact]
    public async Task Handle_Success_CommitsTransaction()
    {
        // Arrange
        var command = CreateValidCommand();
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        SetupSuccessfulRegistration(command, identityUser, expectedTokens);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExceptionThrown_RollsBackTransaction()
    {
        // Arrange
        var command = CreateValidCommand();
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };

        SetupSuccessfulPreChecks(command);
        _identityService.CreateUserAsync(
                command.Name,
                command.Email,
                command.Password,
                Roles.Member,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(identityUser));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    #region Helper Methods

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

    #endregion
}
