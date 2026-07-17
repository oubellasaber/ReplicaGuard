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
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("File name is required.")
            .MaximumLength(255)
            .WithMessage("File name cannot exceed 255 characters.")
            .Must(name => name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            .WithMessage("File name contains invalid characters.")
            .Must(name => !name.Contains('/') && !name.Contains('\\'))
            .WithMessage("File name cannot contain directory separators.");

        RuleFor(x => x.HosterAccountIds)
            .NotEmpty()
            .Must(list => list.Count() <= 10)
            .WithMessage("Too many hoster account IDs specified. Maximum allowed is 10.")
            .Must(list => list.Distinct().Count() == list.Count())
            .WithMessage("Duplicate hoster account IDs are not allowed.");

        RuleForEach(x => x.HosterAccountIds)
            .ChildRules(hoster =>
            {
                hoster.RuleFor(h => h)
                    .NotEmpty()
                    .WithMessage("HosterAccountId cannot be empty.");
            });
    }
}
