using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Assets.ListAssets;
using ReplicaGuard.Domain.Replication;
using Sieve.Models;
using Sieve.Services;

namespace ReplicaGuard.Infrastructure.Filtering;

public sealed class ApplicationSieveProcessor : SieveProcessor
{
    public ApplicationSieveProcessor(IOptions<SieveOptions> options) : base(options)
    {
    }

    protected override SievePropertyMapper MapProperties(SievePropertyMapper mapper)
    {
        mapper
            .MapAssetProperties();

        return mapper;
    }
}

public static class AssetSieveConfiguration
{
    public static SievePropertyMapper MapAssetProperties(this SievePropertyMapper mapper)
    {
        return mapper
            .Map<Asset, AssetSummaryResponse>(a => a.FileName.Value, dto => dto.FileName)
            .Map<Asset, AssetSummaryResponse>(a => a.SizeBytes, dto => dto.SizeBytes)
            .Map<Asset, AssetSummaryResponse>(a => a.CreatedAtUtc, dto => dto.CreatedAtUtc)
            .Map<Asset, AssetSummaryResponse>(a => a.UpdatedAtUtc, dto => dto.UpdatedAtUtc);
    }
}
