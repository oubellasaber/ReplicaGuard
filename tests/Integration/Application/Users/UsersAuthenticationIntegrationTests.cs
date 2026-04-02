using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Exceptions;
using ReplicaGuard.Application.Users;
using ReplicaGuard.Application.Users.LogInUser;
using ReplicaGuard.Application.Users.RefreshToken;
using ReplicaGuard.Application.Users.RegisterUser;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.User;
using ReplicaGuard.TestInfrastructure.Fixtures;
using ReplicaGuard.TestInfrastructure.Infrastructure;
using ReplicaGuard.TestInfrastructure.Utilities;

namespace ReplicaGuard.Application.IntegrationTests.Users;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class UserAuthenticationIntegrationTests
{
    [Fact]
    public async Task Register_Login_Refresh_RotatesRefreshToken_AndSetsDayBasedExpiry()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        AccessTokensResponse registrationTokens;
        string oldRefreshToken;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();

            Result<AccessTokensResponse> registerResult = await sender.Send(
                new RegisterUserCommand("john", "john@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            Result<AccessTokensResponse> loginResult = await sender.Send(
                new LogInUserCommand("john@example.com", "Pass123!"),
                CancellationToken.None);

            registerResult.IsSuccess.Should().BeTrue();
            loginResult.IsSuccess.Should().BeTrue();

            registrationTokens = registerResult.Value;
            oldRefreshToken = loginResult.Value.RefreshToken;
        }

        AccessTokensResponse refreshedTokens;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            Result<AccessTokensResponse> refreshResult = await sender.Send(
                new RefreshTokenCommand(oldRefreshToken),
                CancellationToken.None);

            refreshResult.IsSuccess.Should().BeTrue();
            refreshedTokens = refreshResult.Value;
        }

        // Assert
        registrationTokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        registrationTokens.RefreshToken.Should().NotBeNullOrWhiteSpace();

        refreshedTokens.RefreshToken.Should().NotBe(oldRefreshToken);
        refreshedTokens.AccessToken.Should().NotBeNullOrWhiteSpace();

        using IServiceScope assertScope = harness.ServiceProvider.CreateScope();
        var refreshTokenRepository = assertScope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        RefreshToken? persistedToken = await refreshTokenRepository.GetByTokenAsync(
            refreshedTokens.RefreshToken,
            CancellationToken.None);

        persistedToken.Should().NotBeNull();
        AssertAccessTokenSubject(refreshedTokens.AccessToken, persistedToken!.UserId);
        persistedToken!.ExpiresAtUtc.Should().Be(fixedNow.AddDays(IntegrationHarness.RefreshTokenExpirationInDays));
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsInvalidRefreshTokenError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        string refreshToken;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();

            Result<AccessTokensResponse> registerResult = await sender.Send(
                new RegisterUserCommand("jane", "jane@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            registerResult.IsSuccess.Should().BeTrue();
            refreshToken = registerResult.Value.RefreshToken;
        }

        using (IServiceScope arrangeMutationScope = harness.ServiceProvider.CreateScope())
        {
            await IdentityTestHelper.ExpireRefreshTokenAsync(
                arrangeMutationScope.ServiceProvider,
                refreshToken,
                fixedNow.AddMinutes(-1));
        }

        Result<AccessTokensResponse> refreshResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            refreshResult = await sender.Send(
                new RefreshTokenCommand(refreshToken),
                CancellationToken.None);
        }

        // Assert
        refreshResult.IsFailure.Should().BeTrue();
        refreshResult.Error.Code.Should().Be(AuthenticationErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task LogIn_WithUnknownEmail_ReturnsInvalidCredentialsError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> logInResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            logInResult = await sender.Send(
                new LogInUserCommand("missing@example.com", "Pass123!"),
                CancellationToken.None);
        }

        // Assert
        logInResult.IsFailure.Should().BeTrue();
        logInResult.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task LogIn_WithWrongPassword_ReturnsInvalidCredentialsError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> logInResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            Result<AccessTokensResponse> registerResult = await sender.Send(
                new RegisterUserCommand("john", "john@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            registerResult.IsSuccess.Should().BeTrue();

            // Act
            logInResult = await sender.Send(
                new LogInUserCommand("john@example.com", "WrongPass123!"),
                CancellationToken.None);
        }

        // Assert
        logInResult.IsFailure.Should().BeTrue();
        logInResult.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsInvalidRefreshTokenError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> refreshResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            refreshResult = await sender.Send(
                new RefreshTokenCommand("not-a-real-refresh-token"),
                CancellationToken.None);
        }

        // Assert
        refreshResult.IsFailure.Should().BeTrue();
        refreshResult.Error.Code.Should().Be(AuthenticationErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task Refresh_WhenOldTokenIsReused_ReturnsInvalidRefreshTokenError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        string oldRefreshToken;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();

            Result<AccessTokensResponse> registerResult = await sender.Send(
                new RegisterUserCommand("replay-user", "replay@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            registerResult.IsSuccess.Should().BeTrue();
            oldRefreshToken = registerResult.Value.RefreshToken;
        }

        Result<AccessTokensResponse> firstRefreshResult;
        Result<AccessTokensResponse> secondRefreshResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            firstRefreshResult = await sender.Send(
                new RefreshTokenCommand(oldRefreshToken),
                CancellationToken.None);

            // Act
            secondRefreshResult = await sender.Send(
                new RefreshTokenCommand(oldRefreshToken),
                CancellationToken.None);
        }

        // Assert
        firstRefreshResult.IsSuccess.Should().BeTrue();
        firstRefreshResult.Value.RefreshToken.Should().NotBe(oldRefreshToken);

        secondRefreshResult.IsFailure.Should().BeTrue();
        secondRefreshResult.Error.Code.Should().Be(AuthenticationErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task LogIn_WhenCalledTwice_IssuesDistinctRefreshTokens()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> firstLogInResult;
        Result<AccessTokensResponse> secondLogInResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            Result<AccessTokensResponse> registerResult = await sender.Send(
                new RegisterUserCommand("repeat-login-user", "repeat-login@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            registerResult.IsSuccess.Should().BeTrue();

            firstLogInResult = await sender.Send(
                new LogInUserCommand("repeat-login@example.com", "Pass123!"),
                CancellationToken.None);

            // Act
            secondLogInResult = await sender.Send(
                new LogInUserCommand("repeat-login@example.com", "Pass123!"),
                CancellationToken.None);
        }

        // Assert
        firstLogInResult.IsSuccess.Should().BeTrue();
        secondLogInResult.IsSuccess.Should().BeTrue();
        secondLogInResult.Value.RefreshToken.Should().NotBe(firstLogInResult.Value.RefreshToken);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsIdentityValidationFailedError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> registerResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            registerResult = await sender.Send(
                new RegisterUserCommand("weak-pass-user", "weak-pass@example.com", "123", "123"),
                CancellationToken.None);
        }

        // Assert
        registerResult.IsFailure.Should().BeTrue();
        registerResult.Error.Code.Should().Be("Identity.ValidationFailed");
    }

    [Fact]
    public async Task Register_WithMismatchedConfirmationPassword_ThrowsValidationException()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        // Act
        Func<Task> act = async () =>
        {
            using IServiceScope scope = harness.ServiceProvider.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new RegisterUserCommand("mismatch-user", "mismatch@example.com", "Pass123!", "DifferentPass123!"),
                CancellationToken.None);
        };

        // Assert
        ValidationException exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().Contain(error =>
            error.PropertyName == nameof(RegisterUserCommand.ConfirmationPassword) &&
            error.ErrorMessage == "Passwords do not match");
    }

    [Fact]
    public async Task LogIn_WithInvalidEmailFormat_ThrowsValidationException()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        // Act
        Func<Task> act = async () =>
        {
            using IServiceScope scope = harness.ServiceProvider.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new LogInUserCommand("invalid-email", "Pass123!"),
                CancellationToken.None);
        };

        // Assert
        ValidationException exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().Contain(error =>
            error.PropertyName == nameof(LogInUserCommand.Email) &&
            error.ErrorMessage == "Must be a valid email address");
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsEmailAlreadyTakenError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> duplicateRegistrationResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            Result<AccessTokensResponse> firstRegisterResult = await sender.Send(
                new RegisterUserCommand("john", "john@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            // Act
            duplicateRegistrationResult = await sender.Send(
                new RegisterUserCommand("john-second", "john@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            firstRegisterResult.IsSuccess.Should().BeTrue();
        }

        // Assert
        duplicateRegistrationResult.IsFailure.Should().BeTrue();
        duplicateRegistrationResult.Error.Code.Should().Be(UserErrors.EmailAlreadyTaken(string.Empty).Code);
    }

    [Fact]
    public async Task Register_WithExistingUsername_ReturnsUsernameAlreadyTakenError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> duplicateRegistrationResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            Result<AccessTokensResponse> firstRegisterResult = await sender.Send(
                new RegisterUserCommand("john", "john@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            // Act
            duplicateRegistrationResult = await sender.Send(
                new RegisterUserCommand("john", "another@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            firstRegisterResult.IsSuccess.Should().BeTrue();
        }

        // Assert
        duplicateRegistrationResult.IsFailure.Should().BeTrue();
        duplicateRegistrationResult.Error.Code.Should().Be(UserErrors.UsernameAlreadyTaken(string.Empty).Code);
    }

    private static void AssertAccessTokenSubject(string accessToken, string expectedIdentityUserId)
    {
        var jwt = new JsonWebToken(accessToken);

        jwt.Claims.Should().ContainSingle(claim =>
            claim.Type == JwtRegisteredClaimNames.Sub &&
            claim.Value == expectedIdentityUserId);
    }

}
