using FluentValidation;

namespace ReplicaGuard.Application.Assets.CreateAsset;

internal sealed class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
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
            .WithMessage("File name cannot exceed 255 characters.")
            .Must(name => name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            .WithMessage("File name contains invalid characters.")
            .Must(name => !name.Contains('/') && !name.Contains('\\'))
            .WithMessage("File name cannot contain directory separators.");

        RuleFor(x => x.Hosters)
            .NotEmpty()
            .WithMessage("At least one hoster is required.")
            .Must(list => list.Count <= 10)
            .WithMessage("Too many hosters specified. Maximum allowed is 10.")
            .Must(list => list.Select(h => h.HosterId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate hoster ids are not allowed.")
            .Must(list => list.Select(h => h.HosterAccountId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate hoster account IDs are not allowed.");

        RuleForEach(x => x.Hosters)
            .ChildRules(hoster =>
            {
                hoster.RuleFor(h => h.HosterId)
                    .NotEmpty()
                    .WithMessage("HosterId cannot be empty.");

                hoster.RuleFor(h => h.HosterAccountId)
                    .NotEmpty()
                    .WithMessage("HosterAccountId cannot be empty.");
            });
    }
}
