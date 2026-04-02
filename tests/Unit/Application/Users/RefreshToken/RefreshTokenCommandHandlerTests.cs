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
    private readonly RefreshTokenCommandHandler _sut;

    public RefreshTokenCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _jwtAuthOptionsProvider = Substitute.For<IJwtAuthOptionsProvider>();
        IIdentityUnitOfWork unitOfWork = Substitute.For<IIdentityUnitOfWork>();

        _sut = new RefreshTokenCommandHandler(
            _identityService,
            _dateTimeProvider,
            _tokenProvider,
            _refreshTokenRepository,
            _jwtAuthOptionsProvider,
                unitOfWork);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenTokenIsNotFound()
    {
        // Arrange
        RefreshTokenCommand command = new("missing-token");
        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns((Application.Abstractions.Authentication.RefreshToken?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthenticationErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenTokenIsExpired()
    {
        // Arrange
        RefreshTokenCommand command = new("expired-token");
        var expiredToken = new Application.Abstractions.Authentication.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            Token = command.refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
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
    public async Task Handle_ReturnsTokensAndPersistsUpdatedRefreshToken_WhenTokenIsValid()
    {
        // Arrange
        RefreshTokenCommand command = new("valid-token");
        Application.Abstractions.Authentication.RefreshToken refreshToken = CreateValidRefreshToken(command.refreshToken);
        (string AccessToken, string RefreshToken) newTokens = ("new-access-token", "new-refresh-token");

        DateTime currentTime = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        int expirationInDays = 7;
        DateTime expectedExpiry = currentTime.AddDays(expirationInDays);

        SetupSuccessfulRefresh(command, refreshToken, newTokens, currentTime, expirationInDays);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(newTokens.AccessToken);
        result.Value.RefreshToken.Should().Be(newTokens.RefreshToken);
        refreshToken.Token.Should().Be(newTokens.RefreshToken);
        refreshToken.Token.Should().NotBe(command.refreshToken);
        refreshToken.ExpiresAtUtc.Should().Be(expectedExpiry);
    }

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
        (string AccessToken, string RefreshToken) newTokens,
        DateTime currentTime,
        int expirationInDays)
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
        _dateTimeProvider.UtcNow.Returns(currentTime);
        _jwtAuthOptionsProvider.RefreshTokenExpirationInDays.Returns(expirationInDays);
    }
}
