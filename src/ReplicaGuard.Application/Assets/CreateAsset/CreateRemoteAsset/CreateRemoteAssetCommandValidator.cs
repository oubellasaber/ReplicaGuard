using FluentValidation;

namespace ReplicaGuard.Application.Assets.CreateAsset.CreateRemoteAsset;

internal sealed class CreateRemoteAssetCommandValidator : AbstractValidator<CreateRemoteAssetCommand>
{
    public CreateRemoteAssetCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .HttpUrl();
    }
}
