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
        ("rg@admin.com", "Admin123!", Roles.Admin);
    // Default member users
    public static readonly (string Email, string UserName, string Password, string Role)[] DefaultMembers =
    {
        ("rg@user.com", "User", "User123!", Roles.Member),
    };
}
