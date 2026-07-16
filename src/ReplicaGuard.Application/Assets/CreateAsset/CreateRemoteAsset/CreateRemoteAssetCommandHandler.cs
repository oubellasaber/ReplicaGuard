using MediatR;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Application.Assets.CreateAsset.CreateRemoteAsset;
internal class CreateRemoteAssetCommandHandler(ISender sender) : ICommandHandler<CreateRemoteAssetCommand, CreateAssetResponse>
{
    public Task<Result<CreateAssetResponse>> Handle(CreateRemoteAssetCommand request, CancellationToken cancellationToken)
    {
        var cmd = new CreateAssetCommand(request.Url, request.FileName, request.HosterAccountIds);
        return sender.Send(cmd, cancellationToken);
    }
}
