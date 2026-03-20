using ReplicaGuard.Application.Abstractions.Authentication;

namespace ReplicaGuard.Infrastructure.Seeding;

public static class AppData
{
    // Roles
    public static readonly string[] AppRoles = new[]
    {
        Roles.Admin,
        Roles.Member
    };

    // Hosters To Do
    //public static readonly Dictionary<string, HosterCredentialRequirements> Hosters = new()
    //{
    //    { HosterCodes.Pixeldrain, new HosterCredentialRequirements(false, false, false) },
    //    { HosterCodes.Krakenfiles, new HosterCredentialRequirements(true, false, false) },
    //    { HosterCodes.SendCm, new HosterCredentialRequirements(false, true, false) }
    //};

    // Admin user
    // Admin user
    public static readonly (string Email, string Password, string Role) DefaultAdmin =
        ("admin@local.test", "Admin123!", "admin");

    // Default member users
    public static readonly (string Email, string UserName, string Password, string Role)[] DefaultMembers =
    {
        ("alice@example.com", "Alice", "User123!", Roles.Member),
        ("bob@example.com", "Bob", "User123!", Roles.Member),
        ("carol@example.com", "Carol", "User123!", Roles.Member)
    };
}
