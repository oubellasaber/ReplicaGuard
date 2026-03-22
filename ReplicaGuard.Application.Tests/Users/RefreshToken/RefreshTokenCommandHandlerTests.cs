using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Users;
using ReplicaGuard.Application.Users.RefreshToken;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Application.Tests.Users.RefreshToken;

public class RefreshTokenCommandHandlerTests
{
    private readonly IIdentityService _identityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITokenProvider _tokenProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtAuthOptionsProvider _jwtAuthOptionsProvider;
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly RefreshTokenCommandHandler _sut;

    public RefreshTokenCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _jwtAuthOptionsProvider = Substitute.For<IJwtAuthOptionsProvider>();
        _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

        _sut = new RefreshTokenCommandHandler(
            _identityService,
            _dateTimeProvider,
            _tokenProvider,
            _refreshTokenRepository,
            _jwtAuthOptionsProvider,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsInvalidRefreshTokenError()
    {
        // Arrange
        var command = new RefreshTokenCommand("non-existent-token");
        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns((Application.Abstractions.Authentication.RefreshToken?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthenticationErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsInvalidRefreshTokenError()
    {
        // Arrange
        var command = new RefreshTokenCommand("expired-token");
        var expiredToken = new Application.Abstractions.Authentication.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            Token = command.refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            User = new IdentityUser { Id = "user-123", Email = "john@example.com" }
        };

        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns(expiredToken);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthenticationErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewAccessTokens()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-token");
        var refreshToken = CreateValidRefreshToken(command.refreshToken);
        var newTokens = ("new-access-token", "new-refresh-token");

        SetupSuccessfulRefresh(command, refreshToken, newTokens);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(newTokens.Item1);
        result.Value.RefreshToken.Should().Be(newTokens.Item2);
    }

    [Fact]
    public async Task Handle_Success_UpdatesRefreshTokenInRepository()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-token");
        var refreshToken = CreateValidRefreshToken(command.refreshToken);
        var newTokens = ("new-access-token", "new-refresh-token");

        SetupSuccessfulRefresh(command, refreshToken, newTokens);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepository.Received(1).Update(Arg.Is<Application.Abstractions.Authentication.RefreshToken>(
            rt => rt.Token == newTokens.Item2));
    }

    [Fact]
    public async Task Handle_Success_UpdatesTokenExpiryTime()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-token");
        var refreshToken = CreateValidRefreshToken(command.refreshToken);
        var currentTime = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var expectedExpiry = currentTime.AddMinutes(7); // 7 days in minutes

        _dateTimeProvider.UtcNow.Returns(currentTime);
        _jwtAuthOptionsProvider.RefreshTokenExpirationInDays.Returns(7);
        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns(refreshToken);
        _identityService.GetRolesAsync(refreshToken.User)
            .Returns(new List<string> { "Member" });
        _tokenProvider.Create(refreshToken.UserId, refreshToken.User.Email!, Arg.Any<IEnumerable<string>>())
            .Returns(("new-access-token", "new-refresh-token"));

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepository.Received(1).Update(Arg.Is<Application.Abstractions.Authentication.RefreshToken>(
            rt => rt.ExpiresAtUtc == expectedExpiry));
    }

    [Fact]
    public async Task Handle_Success_SavesChangesToDatabase()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-token");
        var refreshToken = CreateValidRefreshToken(command.refreshToken);
        var newTokens = ("new-access-token", "new-refresh-token");

        SetupSuccessfulRefresh(command, refreshToken, newTokens);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_CreatesTokensWithUserRoles()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-token");
        var refreshToken = CreateValidRefreshToken(command.refreshToken);
        var roles = new List<string> { "Member", "Admin" };

        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns(refreshToken);
        _identityService.GetRolesAsync(refreshToken.User)
            .Returns(roles);
        _tokenProvider.Create(refreshToken.UserId, refreshToken.User.Email!, Arg.Any<IEnumerable<string>>())
            .Returns(("new-access-token", "new-refresh-token"));
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _jwtAuthOptionsProvider.RefreshTokenExpirationInDays.Returns(7);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _tokenProvider.Received(1).Create(
            refreshToken.UserId,
            refreshToken.User.Email!,
            Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(roles)));
    }

    [Fact]
    public async Task Handle_Success_NewTokenIsDifferentFromOldToken()
    {
        // Arrange
        var command = new RefreshTokenCommand("old-token");
        var refreshToken = CreateValidRefreshToken(command.refreshToken);
        var newTokens = ("new-access-token", "new-refresh-token");

        SetupSuccessfulRefresh(command, refreshToken, newTokens);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepository.Received(1).Update(Arg.Is<Application.Abstractions.Authentication.RefreshToken>(
            rt => rt.Token != command.refreshToken && rt.Token == newTokens.Item2));
    }

    #region Helper Methods

    private static Application.Abstractions.Authentication.RefreshToken CreateValidRefreshToken(string token)
    {
        return new Application.Abstractions.Authentication.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            User = new IdentityUser { Id = "user-123", Email = "john@example.com" }
        };
    }

    private void SetupSuccessfulRefresh(
        RefreshTokenCommand command,
        Application.Abstractions.Authentication.RefreshToken refreshToken,
        (string AccessToken, string RefreshToken) newTokens)
    {
        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns(refreshToken);
        _identityService.GetRolesAsync(refreshToken.User)
            .Returns(new List<string> { "Member" });
        _tokenProvider.Create(
                refreshToken.UserId,
                refreshToken.User.Email!,
                Arg.Any<IEnumerable<string>>())
            .Returns(newTokens);
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _jwtAuthOptionsProvider.RefreshTokenExpirationInDays.Returns(7);
    }

    #endregion
}
