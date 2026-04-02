using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.RegisterUser;

namespace ReplicaGuard.Application.Tests.Users.RegisterUser;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _sut = new();

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        // Arrange
        RegisterUserCommand command = CreateCommand();

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenNameIsEmpty(string? name)
    {
        // Arrange
        RegisterUserCommand command = CreateCommand(name: name!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not be empty");
    }

    [Fact]
    public void Validate_Fails_WhenNameExceedsMaxLength()
    {
        // Arrange
        RegisterUserCommand command = CreateCommand(name: new string('a', 257));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_Passes_WhenNameIsAtMaxLength()
    {
        // Arrange
        RegisterUserCommand command = CreateCommand(name: new string('a', 256));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenEmailIsEmpty(string? email)
    {
        // Arrange
        RegisterUserCommand command = CreateCommand(email: email!);

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
        RegisterUserCommand command = CreateCommand(email: email);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Validate_Fails_WhenEmailFormatIsInvalid(string email)
    {
        // Arrange
        RegisterUserCommand command = CreateCommand(email: email);

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
        RegisterUserCommand command = CreateCommand(password: password!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenConfirmationPasswordIsEmpty(string? confirmationPassword)
    {
        // Arrange
        RegisterUserCommand command = CreateCommand(confirmationPassword: confirmationPassword!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmationPassword)
            .WithErrorMessage("Confirmation password must not be empty");
    }

    [Fact]
    public void Validate_Fails_WhenPasswordsDoNotMatch()
    {
        // Arrange
        RegisterUserCommand command = CreateCommand(confirmationPassword: "DifferentPassword!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConfirmationPassword)
            .WithErrorMessage("Passwords do not match");
    }

    private static RegisterUserCommand CreateCommand(
        string name = "JohnDoe",
        string email = "john@example.com",
        string password = "Password123!",
        string confirmationPassword = "Password123!")
    {
        return new RegisterUserCommand(name, email, password, confirmationPassword);
    }
}
