using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Users.LogInUser;
using ReplicaGuard.Domain.Users;

namespace ReplicaGuard.Application.Tests;

public class LogInUserCommandHandlerTests
{
    private readonly IIdentityService _identityService;
    private readonly ITokenProvider _tokenProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly LogInUserCommandHandler _sut;

    public LogInUserCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

        _sut = new LogInUserCommandHandler(
            _identityService,
            _tokenProvider,
            _unitOfWork,
            _refreshTokenRepository);
    }

    [Fact]
    public async Task login_returns_failure_when_user_not_found()
    {
        var command = new LogInUserCommand("john@example.com", "password");
        _identityService.FindByEmailAsync(command.Email)
            .Returns((IdentityUser?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task login_returns_failure_when_password_is_wrong()
    {
        var command = new LogInUserCommand("john@example.com", "wrong-password");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        _identityService.FindByEmailAsync(command.Email).Returns(identityUser);
        _identityService.CheckPasswordAsync(identityUser, command.Password).Returns(false);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task login_returns_tokens_when_credentials_are_valid()
    {
        var command = new LogInUserCommand("john@example.com", "correct-password");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        _identityService.FindByEmailAsync(command.Email).Returns(identityUser);
        _identityService.CheckPasswordAsync(identityUser, command.Password).Returns(true);
        _identityService.GetRolesAsync(identityUser).Returns(new[] { "Member" });
        _tokenProvider.Create(
                identityUser.Id, identityUser.Email!, Arg.Any<IEnumerable<string>>())
            .Returns(expectedTokens);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(expectedTokens.Item1);
        result.Value.RefreshToken.Should().Be(expectedTokens.Item2);
    }
}
