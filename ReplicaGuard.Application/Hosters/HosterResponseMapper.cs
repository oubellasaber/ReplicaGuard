using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.Hosters;

internal static class HosterResponseMapper
{
    public static HosterResponse Map(Hoster hoster)
    {
        List<HosterFeatureRequirementResponse> requirements = hoster.Requirements
            .OrderBy(r => r.Feature)
            .Select(r => new HosterFeatureRequirementResponse(
                MapFeature(r.Feature),
                MapCredentials(r.RequiredAuth)))
            .ToList();

        return new HosterResponse(
            hoster.Id,
            hoster.Code.ToLowerInvariant(),
            hoster.DisplayName,
            MapCredentials(hoster.PrimaryCredentials),
            requirements);
    }

    private static List<string> MapCredentials(Credentials credentials)
    {
        List<string> values = new();

        if ((credentials & Credentials.ApiKey) == Credentials.ApiKey)
            values.Add("apiKey");

        if ((credentials & Credentials.EmailPassword) == Credentials.EmailPassword)
            values.Add("emailPassword");

        if ((credentials & Credentials.UsernamePassword) == Credentials.UsernamePassword)
            values.Add("usernamePassword");

        return values;
    }

    private static string MapFeature(CapabilityCode feature)
    {
        return feature switch
        {
            CapabilityCode.RemoteUpload => "remoteUpload",
            CapabilityCode.SpooledUpload => "spooledUpload",
            CapabilityCode.Download => "download",
            CapabilityCode.CheckStatus => "checkStatus",
            _ => feature.ToString().ToLowerInvariant()
        };
    }
}
