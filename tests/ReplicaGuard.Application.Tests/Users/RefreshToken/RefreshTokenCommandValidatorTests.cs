using FluentAssertions;
using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.RefreshToken;

namespace ReplicaGuard.Application.Tests.Users.RefreshToken;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-refresh-token-12345");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyRefreshToken_ShouldFail(string? token)
    {
        // Arrange
        var command = new RefreshTokenCommand(token!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.refreshToken)
            .WithErrorMessage("Refresh token must not be empty");
    }
}
