using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Seeding;

public class AppSeeder
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AppSeeder> _logger;

    public AppSeeder(
        RoleManager<IdentityRole> roleManager,
        UserManager<IdentityUser> userManager,
        ApplicationDbContext db,
        ILogger<AppSeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedHostersAsync();
        //await SeedUsersAsync();
        //await SeedFakeUsersAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in AppData.AppRoles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private async Task SeedHostersAsync()
    {
        List<string> existingCodes = await _db.Set<Hoster>()
            .AsNoTracking()
            .Select(h => h.Code)
            .ToListAsync();

        foreach (HosterSeed seed in HosterDefinitions.All)
        {
            string normalizedCode = seed.Code.ToUpperInvariant().Trim();

            if (existingCodes.Contains(normalizedCode))
            {
                _logger.LogDebug("Hoster '{Code}' already exists, skipping", seed.Code);
                continue;
            }

            var hosterResult = Hoster.Create(
                seed.Code,
                seed.DisplayName,
                seed.PrimaryCredentials);

            if (hosterResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to create hoster '{Code}': {Error}",
                    seed.Code, hosterResult.Error.Message);
                continue;
            }

            Hoster hoster = hosterResult.Value;

            foreach ((CapabilityCode feature, Credentials requiredAuth) in seed.Features)
            {
                hoster.AddFeatureRequirement(feature, requiredAuth);
            }

            _db.Set<Hoster>().Add(hoster);

            _logger.LogInformation(
                "Seeded hoster '{Code}' ({DisplayName}) with {FeatureCount} features",
                seed.Code, seed.DisplayName, seed.Features.Count);
        }

        await _db.SaveChangesAsync();
    }

    // ---------------- Admin ----------------
    private async Task SeedUsersAsync()
    {
        // Admin user
        var (email, password, role) = AppData.DefaultAdmin;
        if (await _userManager.FindByEmailAsync(email) == null)
        {
            var admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            await _userManager.CreateAsync(admin, password);
            await _userManager.AddToRoleAsync(admin, role);
        }
    }

    // ---------------- Dev Fake Users ----------------
    private async Task SeedFakeUsersAsync()
    {
        var existingCount = await _userManager.Users.CountAsync();
        if (existingCount > 1) return;

        foreach (var member in AppData.DefaultMembers)
        {
            if (await _userManager.FindByEmailAsync(member.Email) == null)
            {
                var user = new IdentityUser
                {
                    UserName = member.UserName,
                    Email = member.Email,
                    EmailConfirmed = true
                };
                await _userManager.CreateAsync(user, member.Password);
                await _userManager.AddToRoleAsync(user, member.Role);
            }
        }
    }
}
