using FluentValidation;

namespace ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;

public class AddHosterCredentialsCommandValidator : AbstractValidator<AddHosterCredentialsCommand>
{
    public AddHosterCredentialsCommandValidator()
    {
        RuleFor(x => x.HosterId)
            .NotEmpty();

        RuleFor(x => x.ApiKey)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.ApiKey))
            .WithMessage("API Key cannot exceed 512 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email cannot exceed 256 characters.")
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Username)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Username))
            .WithMessage("Username cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .MaximumLength(512)
            .When(x => !string.IsNullOrWhiteSpace(x.Password))
            .WithMessage("Password cannot exceed 512 characters.");

        RuleFor(x => x)
            .Custom((command, context) =>
            {
                bool hasApiKey = !string.IsNullOrWhiteSpace(command.ApiKey);
                bool hasEmail = !string.IsNullOrWhiteSpace(command.Email);
                bool hasUsername = !string.IsNullOrWhiteSpace(command.Username);
                bool hasPassword = !string.IsNullOrWhiteSpace(command.Password);

                // Validate that at least one credential type is provided
                if (!hasApiKey && !hasEmail && !hasUsername)
                {
                    context.AddFailure("Credentials", "You must provide valid credentials.");
                    return;
                }

                // Validate that email has a password
                if (hasEmail && !hasPassword)
                {
                    context.AddFailure("Password", "Password is required when email is provided.");
                }

                // Validate that username has a password
                if (hasUsername && !hasPassword)
                {
                    context.AddFailure("Password", "Password is required when username is provided.");
                }

                // Validate that if password is provided, either email or username must also be provided
                if (hasPassword && !hasEmail && !hasUsername)
                {
                    context.AddFailure("Credentials", "Password must be accompanied by either email or username.");
                }
            });
    }
}
