using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Users;
using ReplicaGuard.Application.Users.RefreshToken;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Application.Tests;

public class RefreshTokenCommandHandlerTests
{
    private readonly IIdentityService _identityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITokenProvider _tokenProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtAuthOptionsProvider _jwtOptionsProvider;
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly RefreshTokenCommandHandler _sut;

    public RefreshTokenCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _jwtOptionsProvider = Substitute.For<IJwtAuthOptionsProvider>();
        _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

        _sut = new RefreshTokenCommandHandler(
            _identityService,
            _dateTimeProvider,
            _tokenProvider,
            _refreshTokenRepository,
            _jwtOptionsProvider,
            _unitOfWork);
    }

    [Fact]
    public async Task refresh_returns_failure_when_token_not_found()
    {
        var command = new RefreshTokenCommand("invalid-token");
        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(IdentityErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task refresh_returns_failure_when_token_is_expired()
    {
        var command = new RefreshTokenCommand("expired-token");
        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            Token = "expired-token",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
            User = new IdentityUser { Id = "user-123", Email = "john@example.com" }
        };

        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns(expiredToken);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(IdentityErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task refresh_returns_new_tokens_when_token_is_valid()
    {
        var command = new RefreshTokenCommand("valid-token");
        var now = DateTime.UtcNow;
        _dateTimeProvider.UtcNow.Returns(now);
        _jwtOptionsProvider.RefreshTokenExpirationInDays.Returns(7);

        var identityUser = new IdentityUser { Id = "user-123", Email = "john@example.com" };
        var validToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = "user-123",
            Token = "valid-token",
            ExpiresAtUtc = now.AddDays(1),
            User = identityUser
        };

        _refreshTokenRepository.GetByTokenAsync(command.refreshToken, Arg.Any<CancellationToken>())
            .Returns(validToken);
        _identityService.GetRolesAsync(identityUser).Returns(new[] { "Member" });
        _tokenProvider.Create(
                identityUser.Id, 
                identityUser.Email!,
                Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Member" })))
            .Returns(("new-access-token", "new-refresh-token"));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access-token");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
    }
}
