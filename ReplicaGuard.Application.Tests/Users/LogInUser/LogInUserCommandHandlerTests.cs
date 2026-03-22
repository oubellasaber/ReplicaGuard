using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Users.LogInUser;
using ReplicaGuard.Core.Domain.User;

namespace ReplicaGuard.Application.Tests.Users.LogInUser;

public class LogInUserCommandHandlerTests
{
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly LogInUserCommandHandler _sut;

    public LogInUserCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();

        _sut = new LogInUserCommandHandler(
            _identityService,
            _tokenProvider,
            _unitOfWork,
            _refreshTokenRepository);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsInvalidCredentialsError()
    {
        // Arrange
        var command = new LogInUserCommand("john@example.com", "Password123!");
        _identityService.FindByEmailAsync(command.Email)
            .Returns((IdentityUser?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task Handle_InvalidPassword_ReturnsInvalidCredentialsError()
    {
        // Arrange
        var command = new LogInUserCommand("john@example.com", "WrongPassword!");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };

        _identityService.FindByEmailAsync(command.Email)
            .Returns(identityUser);
        _identityService.CheckPasswordAsync(identityUser, command.Password)
            .Returns(false);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAccessTokens()
    {
        // Arrange
        var command = new LogInUserCommand("john@example.com", "Password123!");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        SetupSuccessfulLogin(command, identityUser, expectedTokens);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(expectedTokens.Item1);
        result.Value.RefreshToken.Should().Be(expectedTokens.Item2);
    }

    [Fact]
    public async Task Handle_Success_AddsRefreshTokenToRepository()
    {
        // Arrange
        var command = new LogInUserCommand("john@example.com", "Password123!");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        SetupSuccessfulLogin(command, identityUser, expectedTokens);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepository.Received(1).Add(expectedTokens.Item2, identityUser.Id);
    }

    [Fact]
    public async Task Handle_Success_SavesChangesToDatabase()
    {
        // Arrange
        var command = new LogInUserCommand("john@example.com", "Password123!");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        SetupSuccessfulLogin(command, identityUser, expectedTokens);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_CreatesTokensWithCorrectRoles()
    {
        // Arrange
        var command = new LogInUserCommand("john@example.com", "Password123!");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var roles = new List<string> { "Member", "Admin" };

        _identityService.FindByEmailAsync(command.Email)
            .Returns(identityUser);
        _identityService.CheckPasswordAsync(identityUser, command.Password)
            .Returns(true);
        _identityService.GetRolesAsync(identityUser)
            .Returns(roles);
        _tokenProvider.Create(identityUser.Id, command.Email, Arg.Any<IEnumerable<string>>())
            .Returns(("access-token", "refresh-token"));

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _tokenProvider.Received(1).Create(
            identityUser.Id,
            command.Email,
            Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(roles)));
    }

    #region Helper Methods

    private void SetupSuccessfulLogin(
        LogInUserCommand command,
        IdentityUser identityUser,
        (string AccessToken, string RefreshToken) tokens)
    {
        _identityService.FindByEmailAsync(command.Email)
            .Returns(identityUser);
        _identityService.CheckPasswordAsync(identityUser, command.Password)
            .Returns(true);
        _identityService.GetRolesAsync(identityUser)
            .Returns(new List<string> { "Member" });
        _tokenProvider.Create(
                identityUser.Id,
                command.Email,
                Arg.Any<IEnumerable<string>>())
            .Returns(tokens);
    }

    #endregion
}
