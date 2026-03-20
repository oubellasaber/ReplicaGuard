using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.GetAsset;

public sealed record GetAssetQuery(Guid AssetId) : IQuery<GetAssetResponse>;
