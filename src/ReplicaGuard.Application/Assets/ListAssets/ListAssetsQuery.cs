using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Assets.ListAssets;

public sealed record ListAssetsQuery(ResourceParameters Parameters) : IQuery<PagedList<AssetSummaryResponse>>;
