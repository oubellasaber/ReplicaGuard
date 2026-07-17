using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.RefreshToken;

namespace ReplicaGuard.Application.Tests;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _sut = new();

    [Fact]
    public void valid_token_passes()
    {
        var command = new RefreshTokenCommand("some-token");
        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_token_fails(string? token)
    {
        var command = new RefreshTokenCommand(token!);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.refreshToken);
    }
}
