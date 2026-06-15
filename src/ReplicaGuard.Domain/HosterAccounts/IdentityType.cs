namespace ReplicaGuard.Core.HosterAccounts;

public enum IdentityType : short
{
    Email = 1,
    Username = 2,
    ApiKey = 3
}

public static class IdentityTypeExtensions
{
    public static bool RequiresValue(this IdentityType type)
    {
        return type switch
        {
            IdentityType.Email => true,
            IdentityType.Username => true,
            IdentityType.ApiKey => false,
            _ => false
        };
    }
}
