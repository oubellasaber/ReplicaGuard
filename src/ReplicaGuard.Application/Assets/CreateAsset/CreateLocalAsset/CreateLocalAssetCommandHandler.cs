using MediatR;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Application.Assets.CreateAsset.CreateLocalAsset;
internal class CreateLocalAssetCommandHandler(ISender sender) : ICommandHandler<CreateLocalAssetCommand, CreateAssetResponse>
{
    public Task<Result<CreateAssetResponse>> Handle(CreateLocalAssetCommand request, CancellationToken cancellationToken)
    {
        var cmd = new CreateAssetCommand(request.FilePath, request.FileName, request.HosterAccountIds, request.AssetId, request.BaseDirectory);
        return sender.Send(cmd, cancellationToken);
    }
}
