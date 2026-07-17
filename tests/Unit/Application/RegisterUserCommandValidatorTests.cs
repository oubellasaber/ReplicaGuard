using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.RegisterUser;

namespace ReplicaGuard.Application.Tests;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _sut = new();

    [Fact]
    public void valid_command_passes()
    {
        var command = new RegisterUserCommand("John", "john@example.com", "Pass123!", "Pass123!");
        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_name_fails(string? name)
    {
        var command = new RegisterUserCommand(name!, "john@example.com", "Pass123!", "Pass123!");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_email_fails(string? email)
    {
        var command = new RegisterUserCommand("John", email!, "Pass123!", "Pass123!");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void invalid_email_format_fails()
    {
        var command = new RegisterUserCommand("John", "not-an-email", "Pass123!", "Pass123!");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_password_fails(string? password)
    {
        var command = new RegisterUserCommand("John", "john@example.com", password!, "Pass123!");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_confirmation_password_fails(string? confirmation)
    {
        var command = new RegisterUserCommand("John", "john@example.com", "Pass123!", confirmation!);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmationPassword);
    }

    [Fact]
    public void password_mismatch_fails()
    {
        var command = new RegisterUserCommand("John", "john@example.com", "Pass123!", "Different456!");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmationPassword);
    }
}
