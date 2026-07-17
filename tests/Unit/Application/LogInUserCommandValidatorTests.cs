using FluentValidation.TestHelper;
using ReplicaGuard.Application.Users.LogInUser;

namespace ReplicaGuard.Application.Tests;

public class LogInUserCommandValidatorTests
{
    private readonly LogInUserCommandValidator _sut = new();

    [Fact]
    public void valid_command_passes()
    {
        var command = new LogInUserCommand("john@example.com", "password");
        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_email_fails(string? email)
    {
        var command = new LogInUserCommand(email!, "password");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_password_fails(string? password)
    {
        var command = new LogInUserCommand("john@example.com", password!);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
