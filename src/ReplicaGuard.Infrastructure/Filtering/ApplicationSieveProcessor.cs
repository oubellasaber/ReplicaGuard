using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Assets.ListAssets;
using ReplicaGuard.Application.HosterAccounts.GetHosterAccounts;
using ReplicaGuard.Domain.HosterAccounts;
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
            .MapAssetProperties()
            .MapHosterAccountProperties();

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

public static class HosterAccountSieveConfiguration
{
    public static SievePropertyMapper MapHosterAccountProperties(this SievePropertyMapper mapper)
    {
        return mapper
            .Map<HosterAccount, HosterAccountSummaryResponse>(a => a.Alias, dto => dto.Alias)
            .Map<HosterAccount, HosterAccountSummaryResponse>(a => a.Description, dto => dto.Description)
            .Map<HosterAccount, HosterAccountSummaryResponse>(a => a.Hoster.Code, dto => dto.HosterCode)
            .Map<HosterAccount, HosterAccountSummaryResponse>(a => a.CreatedAtUtc, dto => dto.CreatedAtUtc)
            .Map<HosterAccount, HosterAccountSummaryResponse>(a => a.UpdatedAtUtc, dto => dto.UpdatedAtUtc);
    }
}
