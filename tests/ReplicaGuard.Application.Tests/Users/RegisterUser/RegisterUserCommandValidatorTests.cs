using FluentAssertions;
using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.RegisterUser;

namespace ReplicaGuard.Application.Tests.Users.RegisterUser;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "JohnDoe",
            "john@example.com",
            "SecurePassword123!",
            "SecurePassword123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyName_ShouldFail(string? name)
    {
        // Arrange
        var command = new RegisterUserCommand(
            name!,
            "john@example.com",
            "Password123!",
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not be empty");
    }

    [Fact]
    public void Validate_NameExceeds256Chars_ShouldFail()
    {
        // Arrange
        var command = new RegisterUserCommand(
            new string('a', 257),
            "john@example.com",
            "Password123!",
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameIsExactly256Chars_ShouldPass()
    {
        // Arrange
        var command = new RegisterUserCommand(
            new string('a', 256),
            "john@example.com",
            "Password123!",
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyEmail_ShouldFail(string? email)
    {
        // Arrange
        var command = new RegisterUserCommand(
            "JohnDoe",
            email!,
            "Password123!",
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
        var email = new string('a', 245) + "@example.com";
        var command = new RegisterUserCommand(
            "JohnDoe",
            email,
            "Password123!",
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    //[InlineData("invalid@.com")]
    public void Validate_InvalidEmailFormat_ShouldFail(string email)
    {
        // Arrange
        var command = new RegisterUserCommand(
            "JohnDoe",
            email,
            "Password123!",
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
        var command = new RegisterUserCommand(
            "JohnDoe",
            "john@example.com",
            password!,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyConfirmationPassword_ShouldFail(string? confirmationPassword)
    {
        // Arrange
        var command = new RegisterUserCommand(
            "JohnDoe",
            "john@example.com",
            "Password123!",
            confirmationPassword!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmationPassword)
            .WithErrorMessage("Confirmation password must not be empty");
    }

    [Fact]
    public void Validate_PasswordsMismatch_ShouldFail()
    {
        // Arrange
        var command = new RegisterUserCommand(
            "JohnDoe",
            "john@example.com",
            "Password123!",
            "DifferentPassword!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmationPassword)
            .WithErrorMessage("Passwords do not match");
    }
}
