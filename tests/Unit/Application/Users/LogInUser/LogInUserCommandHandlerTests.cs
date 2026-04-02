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
    private readonly LogInUserCommandHandler _sut;

    public LogInUserCommandHandlerTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _tokenProvider = Substitute.For<ITokenProvider>();
        IIdentityUnitOfWork unitOfWork = Substitute.For<IIdentityUnitOfWork>();
        IRefreshTokenRepository refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();

        _sut = new LogInUserCommandHandler(
            _identityService,
            _tokenProvider,
            unitOfWork,
            refreshTokenRepository);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenUserDoesNotExist()
    {
        // Arrange
        LogInUserCommand command = CreateCommand();
        _identityService.FindByEmailAsync(command.Email)
            .Returns((IdentityUser?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenPasswordIsInvalid()
    {
        // Arrange
        LogInUserCommand command = new("john@example.com", "WrongPassword!");
        IdentityUser identityUser = CreateIdentityUser(command.Email);

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
    public async Task Handle_ReturnsTokensAndPersistsRefreshToken_WhenCredentialsAreValid()
    {
        // Arrange
        LogInUserCommand command = CreateCommand();
        IdentityUser identityUser = CreateIdentityUser(command.Email);
        (string AccessToken, string RefreshToken) tokens = ("access-token", "refresh-token");

        SetupSuccessfulLogin(command, identityUser, tokens);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(tokens.AccessToken);
        result.Value.RefreshToken.Should().Be(tokens.RefreshToken);
    }

    private static LogInUserCommand CreateCommand() =>
        new("john@example.com", "Password123!");

    private static IdentityUser CreateIdentityUser(string email) =>
        new() { Id = "user-123", Email = email };

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
}
