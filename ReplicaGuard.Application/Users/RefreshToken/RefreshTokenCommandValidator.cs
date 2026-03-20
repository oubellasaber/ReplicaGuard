using FluentValidation;

namespace ReplicaGuard.Application.Users.RefreshToken;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.refreshToken)
            .NotEmpty()
            .WithMessage("Refresh token must not be empty");
    }
}
