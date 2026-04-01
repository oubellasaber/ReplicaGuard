using FluentValidation;

namespace ReplicaGuard.Application.Assets.CreateAsset;

public sealed class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetCommandValidator()
    {
        RuleFor(x => x.Source)
            .NotEmpty()
            .WithMessage("Source is required.")
            .MaximumLength(2048)
            .WithMessage("Source cannot exceed 2048 characters.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required.")
            .MaximumLength(255)
            .WithMessage("File name cannot exceed 255 characters.");

        RuleFor(x => x.HosterIds)
            .NotEmpty()
            .WithMessage("At least one hoster is required.");

        RuleForEach(x => x.HosterIds)
            .NotEmpty()
            .WithMessage("Hoster ID cannot be empty.");

        RuleFor(x => x.HosterIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .When(x => x.HosterIds is { Count: > 0 })
            .WithMessage("Duplicate hoster IDs are not allowed.");
    }
}
