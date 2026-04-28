namespace ReplicaGuard.Core.Domain.Hoster;

/// <summary>
/// Each hoster implements this. Carries its own seed data.
/// The seeder scans for all implementations at startup.
/// </summary>
public interface IHosterDefinition
{
    static abstract string Code { get; }
    static abstract string DisplayName { get; }
    static abstract Credentials PrimaryCredentials { get; }
    static abstract Credentials SecondaryCredentials { get; }
    static abstract IReadOnlyList<(CapabilityCode Feature, Credentials RequiredAuth)> Features { get; }
}

public interface IHosterApiClient
{
    string Code { get; }
}

public readonly struct Pixeldrain : IHosterDefinition
{
    public static string Code => "pixeldrain";
    public static string DisplayName => "Pixeldrain";
    public static Credentials PrimaryCredentials => Credentials.ApiKey;
    public static Credentials SecondaryCredentials => Credentials.None;
    public static IReadOnlyList<(CapabilityCode, Credentials)> Features =>
    [
        (CapabilityCode.SpooledUpload, Credentials.ApiKey)
    ];
}

public readonly struct SendCm : IHosterDefinition
{
    public static string Code => "sendcm";
    public static string DisplayName => "Send.CM";
    public static Credentials PrimaryCredentials => Credentials.ApiKey;
    public static Credentials SecondaryCredentials => Credentials.None;
    public static IReadOnlyList<(CapabilityCode, Credentials)> Features =>
    [
        (CapabilityCode.RemoteUpload, Credentials.ApiKey),
        (CapabilityCode.SpooledUpload, Credentials.ApiKey)
    ];
}

/// <summary>
/// Scans all IHosterDefinition implementations and extracts their seed data.
/// </summary>
public static class HosterDefinitions
{
    public static IReadOnlyList<HosterSeed> All { get; } = Scan();

    private static List<HosterSeed> Scan()
    {
        return typeof(IHosterDefinition).Assembly
            .GetTypes()
            .Where(t => t is { IsValueType: true, IsAbstract: false } &&
                       t.GetInterfaces().Any(i => i == typeof(IHosterDefinition)))
            .Select(t => new HosterSeed(
                Code: ((string)t.GetProperty(nameof(IHosterDefinition.Code))!.GetValue(null)!).ToUpperInvariant(),
                DisplayName: (string)t.GetProperty(nameof(IHosterDefinition.DisplayName))!.GetValue(null)!,
                PrimaryCredentials: (Credentials)t.GetProperty(nameof(IHosterDefinition.PrimaryCredentials))!.GetValue(null)!,
                Features: (IReadOnlyList<(CapabilityCode, Credentials)>)t.GetProperty(nameof(IHosterDefinition.Features))!.GetValue(null)!))
            .ToList();
    }
}

public sealed record HosterSeed(
    string Code,
    string DisplayName,
    Credentials PrimaryCredentials,
    IReadOnlyList<(CapabilityCode Feature, Credentials RequiredAuth)> Features);
