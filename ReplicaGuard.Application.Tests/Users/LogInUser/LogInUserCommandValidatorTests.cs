using FluentAssertions;
using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.LogInUser;

namespace ReplicaGuard.Application.Tests.Users.LogInUser;

public class LogInUserCommandValidatorTests
{
    private readonly LogInUserCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new LogInUserCommand(
            "john@example.com",
            "SecurePassword123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyEmail_ShouldFail(string? email)
    {
        // Arrange
        var command = new LogInUserCommand(
            email!,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not be empty");
    }

    [Fact]
    public void Validate_EmailExceeds256Chars_ShouldFail()
    {
        // Arrange
        var email = new string('a', 245) + "@example.com"; // 257 chars
        var command = new LogInUserCommand(
            email,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email can not exceed 256 characters");
    }

    [Fact]
    public void Validate_EmailIsExactly256Chars_ShouldPass()
    {
        // Arrange
        var email = new string('a', 244) + "@example.com"; // 256 chars
        var command = new LogInUserCommand(
            email,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Validate_InvalidEmailFormat_ShouldFail(string email)
    {
        // Arrange
        var command = new LogInUserCommand(
            email,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Must be a valid email address");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyPassword_ShouldFail(string? password)
    {
        // Arrange
        var command = new LogInUserCommand(
            "john@example.com",
            password!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not be empty");
    }
}
