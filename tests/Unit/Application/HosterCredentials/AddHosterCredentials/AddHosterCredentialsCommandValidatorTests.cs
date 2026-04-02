using FluentValidation.TestHelper;
using ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;

namespace ReplicaGuard.Application.Tests.HosterCredentials.AddHosterCredentials;

public class AddHosterCredentialsCommandValidatorTests
{
    private readonly AddHosterCredentialsCommandValidator _sut = new();

    [Fact]
    public void Validate_Passes_WhenApiKeyPayloadIsValid()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(apiKey: "api-key");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_Passes_WhenEmailPasswordPayloadIsValid()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(email: "john@example.com", password: "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_Fails_WhenHosterIdIsEmpty()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(hosterId: Guid.Empty, apiKey: "api-key");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HosterId);
    }

    [Fact]
    public void Validate_Fails_WhenApiKeyExceedsMaxLength()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(apiKey: new string('a', 513));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ApiKey)
            .WithErrorMessage("API Key cannot exceed 512 characters.");
    }

    [Fact]
    public void Validate_Fails_WhenEmailExceedsMaxLength()
    {
        // Arrange
        string email = new string('a', 245) + "@example.com";
        AddHosterCredentialsCommand command = CreateCommand(email: email, password: "Password123!");

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
    public void Validate_Fails_WhenEmailFormatIsInvalid(string email)
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(email: email, password: "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void Validate_Fails_WhenUsernameExceedsMaxLength()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(username: new string('a', 257), password: "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username cannot exceed 256 characters.");
    }

    [Fact]
    public void Validate_Fails_WhenPasswordExceedsMaxLength()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(email: "john@example.com", password: new string('a', 513));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password cannot exceed 512 characters.");
    }

    [Fact]
    public void Validate_Fails_WhenNoCredentialsAreProvided()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand();

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Credentials")
            .WithErrorMessage("You must provide valid credentials.");
    }

    [Fact]
    public void Validate_Fails_WhenEmailIsProvidedWithoutPassword()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(email: "john@example.com");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Password")
            .WithErrorMessage("Password is required when email is provided.");
    }

    [Fact]
    public void Validate_Fails_WhenUsernameIsProvidedWithoutPassword()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(username: "john");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Password")
            .WithErrorMessage("Password is required when username is provided.");
    }

    [Fact]
    public void Validate_Fails_WhenPasswordHasNoEmailOrUsername()
    {
        // Arrange
        AddHosterCredentialsCommand command = CreateCommand(apiKey: "api-key", password: "Password123!");

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Credentials")
            .WithErrorMessage("Password must be accompanied by either email or username.");
    }

    private static AddHosterCredentialsCommand CreateCommand(
        Guid? hosterId = null,
        string? apiKey = null,
        string? username = null,
        string? email = null,
        string? password = null)
    {
        return new AddHosterCredentialsCommand(
            hosterId ?? Guid.NewGuid(),
            apiKey,
            username,
            email,
            password);
    }
}
