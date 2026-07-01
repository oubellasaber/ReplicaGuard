namespace ReplicaGuard.Domain.Hosters;

public enum HosterCode : short
{
    [FriendlyString("pixeldrain")]
    Pixeldrain = 1,

    [FriendlyString("sendcm")]
    SendCm = 2,
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class FriendlyStringAttribute : Attribute
{
    public string Name { get; }
    public FriendlyStringAttribute(string name) => Name = name;
}

public static class HosterCodeExtensions
{
    public static string ToFriendlyString(this HosterCode code)
    {
        var field = typeof(HosterCode).GetField(code.ToString());
        var attr = field?.GetCustomAttributes(typeof(FriendlyStringAttribute), false)
                         .Cast<FriendlyStringAttribute>()
                         .FirstOrDefault();

        return attr?.Name ?? code.ToString().ToLowerInvariant();
    }

    public static HosterCode FromFriendlyString(string apiValue)
    {
        foreach (var field in typeof(HosterCode).GetFields())
        {
            var attr = field.GetCustomAttributes(typeof(FriendlyStringAttribute), false)
                            .Cast<FriendlyStringAttribute>()
                            .FirstOrDefault();

            if (attr != null && attr.Name.Equals(apiValue, StringComparison.OrdinalIgnoreCase))
                return (HosterCode)field.GetValue(null)!;
        }

        throw new ArgumentException($"Unknown hoster: {apiValue}");
    }
}
