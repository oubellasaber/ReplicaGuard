using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.RefreshToken;

namespace ReplicaGuard.Application.Tests.Users.RefreshToken;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _sut = new();

    [Fact]
    public void Validate_Passes_WhenRefreshTokenIsProvided()
    {
        // Arrange
        RefreshTokenCommand command = new("valid-refresh-token-12345");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenRefreshTokenIsEmpty(string? token)
    {
        // Arrange
        RefreshTokenCommand command = new(token!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.refreshToken)
            .WithErrorMessage("Refresh token must not be empty");
    }
}
