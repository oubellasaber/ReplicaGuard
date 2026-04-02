using FluentValidation;

namespace ReplicaGuard.Application.Users.RegisterUser;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name must not be empty")
            .MaximumLength(256)
            .WithMessage("Name can not exceed 256 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email must not be empty")
            .MaximumLength(256)
            .WithMessage("Email can not exceed 256 characters")
            .EmailAddress()
            .WithMessage("Must be a valid email address");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password must not be empty");

        RuleFor(x => x.ConfirmationPassword)
            .NotEmpty()
            .WithMessage("Confirmation password must not be empty")
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match");
    }
}
