using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.ListAssets;

public sealed record ListAssetsQuery : IQuery<List<AssetSummaryResponse>>;
