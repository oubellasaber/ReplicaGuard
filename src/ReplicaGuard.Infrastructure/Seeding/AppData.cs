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

    // Admin user
    public static readonly (string Email, string Password, string Role) DefaultAdmin =
        ("admin@local.test", "Admin123!", Roles.Admin);
    // Default member users
    public static readonly (string Email, string UserName, string Password, string Role)[] DefaultMembers =
    {
        ("alice@example.com", "Alice", "User123!", Roles.Member),
        ("bob@example.com", "Bob", "User123!", Roles.Member),
        ("carol@example.com", "Carol", "User123!", Roles.Member)
    };
}
