using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Exceptions;
using ReplicaGuard.Application.Users;
using ReplicaGuard.Application.Users.LogInUser;
using ReplicaGuard.Application.Users.RefreshToken;
using ReplicaGuard.Application.Users.RegisterUser;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Users;
using ReplicaGuard.TestInfrastructure.Fixtures;
using ReplicaGuard.TestInfrastructure.Infrastructure;
using ReplicaGuard.TestInfrastructure.Utilities;

namespace ReplicaGuard.Application.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class UserAuthenticationIntegrationTests
{
    [Fact]
    public async Task register_login_refresh_rotates_token_and_sets_day_based_expiry()
    {
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

            Result<AccessTokensResponse> refreshResult = await sender.Send(
                new RefreshTokenCommand(oldRefreshToken),
                CancellationToken.None);

            refreshResult.IsSuccess.Should().BeTrue();
            refreshedTokens = refreshResult.Value;
        }

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
    public async Task refresh_with_expired_token_returns_invalid_refresh_token()
    {
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

            refreshResult = await sender.Send(
                new RefreshTokenCommand(refreshToken),
                CancellationToken.None);
        }

        refreshResult.IsFailure.Should().BeTrue();
        refreshResult.Error.Code.Should().Be(IdentityErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task login_with_unknown_email_returns_invalid_credentials()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> logInResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            logInResult = await sender.Send(
                new LogInUserCommand("missing@example.com", "Pass123!"),
                CancellationToken.None);
        }

        logInResult.IsFailure.Should().BeTrue();
        logInResult.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task login_with_wrong_password_returns_invalid_credentials()
    {
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

            logInResult = await sender.Send(
                new LogInUserCommand("john@example.com", "WrongPass123!"),
                CancellationToken.None);
        }

        logInResult.IsFailure.Should().BeTrue();
        logInResult.Error.Code.Should().Be(UserErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task refresh_with_unknown_token_returns_invalid_refresh_token()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> refreshResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            refreshResult = await sender.Send(
                new RefreshTokenCommand("not-a-real-refresh-token"),
                CancellationToken.None);
        }

        refreshResult.IsFailure.Should().BeTrue();
        refreshResult.Error.Code.Should().Be(IdentityErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task refresh_when_old_token_is_reused_returns_invalid_refresh_token()
    {
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

            secondRefreshResult = await sender.Send(
                new RefreshTokenCommand(oldRefreshToken),
                CancellationToken.None);
        }

        firstRefreshResult.IsSuccess.Should().BeTrue();
        firstRefreshResult.Value.RefreshToken.Should().NotBe(oldRefreshToken);

        secondRefreshResult.IsFailure.Should().BeTrue();
        secondRefreshResult.Error.Code.Should().Be(IdentityErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task login_when_called_twice_issues_distinct_refresh_tokens()
    {
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

            secondLogInResult = await sender.Send(
                new LogInUserCommand("repeat-login@example.com", "Pass123!"),
                CancellationToken.None);
        }

        firstLogInResult.IsSuccess.Should().BeTrue();
        secondLogInResult.IsSuccess.Should().BeTrue();
        secondLogInResult.Value.RefreshToken.Should().NotBe(firstLogInResult.Value.RefreshToken);
    }

    [Fact]
    public async Task register_with_weak_password_returns_validation_failed()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AccessTokensResponse> registerResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            registerResult = await sender.Send(
                new RegisterUserCommand("weak-pass-user", "weak-pass@example.com", "123", "123"),
                CancellationToken.None);
        }

        registerResult.IsFailure.Should().BeTrue();
        registerResult.Error.Code.Should().Be("Identity.ValidationFailed");
    }

    [Fact]
    public async Task register_with_mismatched_confirmation_password_throws_validation_exception()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Func<Task> act = async () =>
        {
            using IServiceScope scope = harness.ServiceProvider.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new RegisterUserCommand("mismatch-user", "mismatch@example.com", "Pass123!", "DifferentPass123!"),
                CancellationToken.None);
        };

        var exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().Contain(error =>
            error.PropertyName == nameof(RegisterUserCommand.ConfirmationPassword) &&
            error.ErrorMessage == "Passwords do not match");
    }

    [Fact]
    public async Task login_with_invalid_email_format_throws_validation_exception()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Func<Task> act = async () =>
        {
            using IServiceScope scope = harness.ServiceProvider.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new LogInUserCommand("invalid-email", "Pass123!"),
                CancellationToken.None);
        };

        var exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().Contain(error =>
            error.PropertyName == nameof(LogInUserCommand.Email) &&
            error.ErrorMessage == "Must be a valid email address");
    }

    [Fact]
    public async Task register_with_existing_email_returns_email_already_taken()
    {
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

            duplicateRegistrationResult = await sender.Send(
                new RegisterUserCommand("john-second", "john@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            firstRegisterResult.IsSuccess.Should().BeTrue();
        }

        duplicateRegistrationResult.IsFailure.Should().BeTrue();
        duplicateRegistrationResult.Error.Code.Should().Be(UserErrors.EmailAlreadyTaken(string.Empty).Code);
    }

    [Fact]
    public async Task register_with_existing_username_returns_username_already_taken()
    {
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

            duplicateRegistrationResult = await sender.Send(
                new RegisterUserCommand("john", "another@example.com", "Pass123!", "Pass123!"),
                CancellationToken.None);

            firstRegisterResult.IsSuccess.Should().BeTrue();
        }

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
