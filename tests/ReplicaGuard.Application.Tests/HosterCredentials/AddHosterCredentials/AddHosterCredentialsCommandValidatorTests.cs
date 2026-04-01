using FluentValidation.TestHelper;
using ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;

namespace ReplicaGuard.Application.Tests.HosterCredentials.AddHosterCredentials;

public class AddHosterCredentialsCommandValidatorTests
{
    private readonly AddHosterCredentialsCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidApiKeyCommand_ShouldPass()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            "api-key",
            null,
            null,
            null);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidEmailPasswordCommand_ShouldPass()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            null,
            "john@example.com",
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyHosterId_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.Empty,
            "api-key",
            null,
            null,
            null);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HosterId);
    }

    [Fact]
    public void Validate_ApiKeyExceeds512Chars_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            new string('a', 513),
            null,
            null,
            null);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ApiKey)
            .WithErrorMessage("API Key cannot exceed 512 characters.");
    }

    [Fact]
    public void Validate_EmailExceeds256Chars_ShouldFail()
    {
        // Arrange
        string email = new string('a', 245) + "@example.com";
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            null,
            email,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email cannot exceed 256 characters.");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Validate_InvalidEmailFormat_ShouldFail(string email)
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            null,
            email,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void Validate_UsernameExceeds256Chars_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            new string('a', 257),
            null,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username cannot exceed 256 characters.");
    }

    [Fact]
    public void Validate_PasswordExceeds512Chars_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            null,
            "john@example.com",
            new string('a', 513));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password cannot exceed 512 characters.");
    }

    [Fact]
    public void Validate_NoCredentialsProvided_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            null,
            null,
            null);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Credentials")
            .WithErrorMessage("You must provide valid credentials.");
    }

    [Fact]
    public void Validate_EmailWithoutPassword_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            null,
            "john@example.com",
            null);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Password")
            .WithErrorMessage("Password is required when email is provided.");
    }

    [Fact]
    public void Validate_UsernameWithoutPassword_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            null,
            "john",
            null,
            null);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Password")
            .WithErrorMessage("Password is required when username is provided.");
    }

    [Fact]
    public void Validate_PasswordWithoutEmailOrUsername_ShouldFail()
    {
        // Arrange
        var command = new AddHosterCredentialsCommand(
            Guid.NewGuid(),
            "api-key",
            null,
            null,
            "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Credentials")
            .WithErrorMessage("Password must be accompanied by either email or username.");
    }
}
