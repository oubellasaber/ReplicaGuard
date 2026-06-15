using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Core.Users;
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
        var seedingTasks = new[] { SeedHostersAsync(), SeedFakeUsersAsync() };
        await Task.WhenAll(seedingTasks);
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
        var existing = await _db.Set<Hoster>().ToListAsync();

        foreach (var def in HosterDefinitions.All)
        {
            var hoster = existing.SingleOrDefault(h => h.Id == def.HosterId);

            if (hoster is null)
            {
                // Insert new hoster
                hoster = new Hoster(def.HosterId, def.HosterId.ToString());

                _db.Set<Hoster>().Add(hoster);

                _logger.LogInformation("Seeded hoster {HosterId}", def.HosterId);
            }
            //else
            //{
            //    // Update display name if needed
            //    if (hoster.DisplayName != def.HosterId.ToString())
            //    {
            //        hoster.UpdateDisplayName(def.HosterId.ToString());
            //        _logger.LogInformation("Updated hoster {HosterId}", def.HosterId);
            //    }
            //}
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedFakeUsersAsync()
    {
        var existing = await _userManager.Users
            .Select(u => new { u.Id, u.Email })
            .ToListAsync();

        var emails = existing
            .Select(u => u.Email!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ---- Admin ----
        {
            var (email, password, role) = AppData.DefaultAdmin;
            IdentityUser? admin;

            if (!emails.Contains(email))
            {
                admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                await _userManager.CreateAsync(admin, password);
            }
            else
            {
                var id = existing.First(u => u.Email == email).Id;
                admin = await _userManager.FindByIdAsync(id);
            }

            if (admin is not null && !await _userManager.IsInRoleAsync(admin, role))
                await _userManager.AddToRoleAsync(admin, role);

            // ---- Create domain user ----
            if (admin is not null)
            {
                var exists = await _db.Set<User>()
                    .AnyAsync(u => u.IdentityId == admin.Id);

                if (!exists)
                {
                    var domainUser = User.Create(
                        identityId: admin.Id,
                        email: admin.Email!,
                        name: admin.UserName!,
                        createdAtUtc: DateTime.UtcNow
                    );

                    _db.Set<User>().Add(domainUser);
                    await _db.SaveChangesAsync();
                }
            }
        }

    }
}
