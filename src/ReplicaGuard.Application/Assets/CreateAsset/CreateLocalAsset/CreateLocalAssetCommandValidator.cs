using FluentValidation;

namespace ReplicaGuard.Application.Assets.CreateAsset.CreateLocalAsset;
internal sealed class CreateLocalAssetCommandValidator : AbstractValidator<CreateLocalAssetCommand>
{
    public CreateLocalAssetCommandValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .ValidFilePath();
    }
}
