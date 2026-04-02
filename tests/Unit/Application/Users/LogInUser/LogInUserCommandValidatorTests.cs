using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.LogInUser;

namespace ReplicaGuard.Application.Tests.Users.LogInUser;

public class LogInUserCommandValidatorTests
{
    private readonly LogInUserCommandValidator _sut = new();

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        // Arrange
        LogInUserCommand command = CreateCommand();

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenEmailIsEmpty(string? email)
    {
        // Arrange
        LogInUserCommand command = CreateCommand(email: email!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not be empty");
    }

    [Fact]
    public void Validate_Fails_WhenEmailExceedsMaxLength()
    {
        // Arrange
        string email = new string('a', 245) + "@example.com";
        LogInUserCommand command = CreateCommand(email: email);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email can not exceed 256 characters");
    }

    [Fact]
    public void Validate_Passes_WhenEmailIsAtMaxLength()
    {
        // Arrange
        string email = new string('a', 244) + "@example.com";
        LogInUserCommand command = CreateCommand(email: email);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Validate_Fails_WhenEmailFormatIsInvalid(string email)
    {
        // Arrange
        LogInUserCommand command = CreateCommand(email: email);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Must be a valid email address");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenPasswordIsEmpty(string? password)
    {
        // Arrange
        LogInUserCommand command = CreateCommand(password: password!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not be empty");
    }

    private static LogInUserCommand CreateCommand(
        string email = "john@example.com",
        string password = "SecurePassword123!")
    {
        return new LogInUserCommand(email, password);
    }
}
