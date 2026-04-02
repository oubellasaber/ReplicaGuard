using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Users;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Infrastructure.Authentication;

namespace ReplicaGuard.Infrastructure.Identity;

internal class IdentityService : IIdentityService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly CrossContextUnitOfWork _identityDbContext;
    private readonly JwtAuthOptions _jwtAuthOptions;

    public IdentityService(
        UserManager<IdentityUser> userManager,
        CrossContextUnitOfWork identityDbContext,
        IOptions<JwtAuthOptions> jwtAuthOptions)
    {
        _userManager = userManager;
        _identityDbContext = identityDbContext;
        _jwtAuthOptions = jwtAuthOptions.Value;
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return await _userManager.FindByEmailAsync(email) is not null;
    }

    public async Task<bool> UsernameExistsAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        return await _userManager.FindByNameAsync(username) is not null;
    }

    public async Task<Result<IdentityUser>> CreateUserAsync(
        string username,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        IdentityUser identityUser = new()
        {
            UserName = username,
            Email = email,
        };

        IdentityResult createResult = await _userManager.CreateAsync(identityUser, password);

        if (!createResult.Succeeded)
        {
            return Result.Failure<IdentityUser>(AuthenticationErrors.FromIdentityErrors(createResult.Errors));
        }

        IdentityResult roleResult = await _userManager.AddToRoleAsync(identityUser, role);

        if (!roleResult.Succeeded)
        {
            return Result.Failure<IdentityUser>(AuthenticationErrors.FromIdentityErrors(createResult.Errors));
        }

        return Result.Success(identityUser);
    }

    public Task<IdentityUser?> FindByEmailAsync(string email)
        => _userManager.FindByEmailAsync(email);

    public Task<bool> CheckPasswordAsync(IdentityUser identityUser, string password)
        => _userManager.CheckPasswordAsync(identityUser, password);


    public Task<IList<string>> GetRolesAsync(IdentityUser identityUser)
        => _userManager.GetRolesAsync(identityUser);
}
