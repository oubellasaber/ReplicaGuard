using FluentValidation;

namespace ReplicaGuard.Application.HosterCredentials.UpdateHosterCredentials;

public class UpdateHosterCredentialsCommandValidator : AbstractValidator<UpdateHosterCredentialsCommand>
{
    public UpdateHosterCredentialsCommandValidator()
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

                // Validate that at least one credential is being updated
                if (!hasApiKey && !hasEmail && !hasUsername && !hasPassword)
                {
                    context.AddFailure("Credentials", "At least one credential field must be provided for update.");
                    return;
                }

                // Validate that if password is provided alone, it's valid (updating password for existing email/username)
                // But if email or username is newly provided, password must accompany it
                if (hasEmail && !hasPassword)
                {
                    context.AddFailure("Password", "Password is required when updating email.");
                }

                if (hasUsername && !hasPassword)
                {
                    context.AddFailure("Password", "Password is required when updating username.");
                }
            });
    }
}
