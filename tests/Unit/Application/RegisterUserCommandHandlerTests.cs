using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Users.RegisterUser;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Users;

namespace ReplicaGuard.Application.Tests;

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
    public async Task registration_returns_failure_when_email_is_already_taken()
    {
        var command = new RegisterUserCommand("John", "john@example.com", "Pass123!", "Pass123!");
        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.EmailAlreadyTaken(command.Email).Code);
    }

    [Fact]
    public async Task registration_returns_failure_when_username_is_already_taken()
    {
        var command = new RegisterUserCommand("John", "john@example.com", "Pass123!", "Pass123!");
        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);
        _identityService.UsernameExistsAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.UsernameAlreadyTaken(command.Name).Code);
    }

    [Fact]
    public async Task registration_rolls_back_and_returns_failure_when_identity_creation_fails()
    {
        var command = new RegisterUserCommand("John", "john@example.com", "Pass123!", "Pass123!");
        var identityError = new Error("Identity.Failure", "Password too weak");

        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>()).Returns(false);
        _identityService.UsernameExistsAsync(command.Name, Arg.Any<CancellationToken>()).Returns(false);
        _identityService.CreateUserAsync(
                command.Name, command.Email, command.Password,
                Roles.Member, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IdentityUser>(identityError));

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(identityError);
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task registration_creates_user_and_commits_transaction_when_request_is_valid()
    {
        var command = new RegisterUserCommand("John", "john@example.com", "Pass123!", "Pass123!");
        var identityUser = new IdentityUser { Id = "user-123", Email = command.Email };
        var expectedTokens = ("access-token", "refresh-token");

        _identityService.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>()).Returns(false);
        _identityService.UsernameExistsAsync(command.Name, Arg.Any<CancellationToken>()).Returns(false);
        _identityService.CreateUserAsync(
                command.Name, command.Email, command.Password,
                Roles.Member, Arg.Any<CancellationToken>())
            .Returns(Result.Success(identityUser));
        _tokenProvider.Create(
                identityUser.Id, identityUser.Email!, Arg.Any<IEnumerable<string>>())
            .Returns(expectedTokens);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(expectedTokens.Item1);
        result.Value.RefreshToken.Should().Be(expectedTokens.Item2);
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }
}
