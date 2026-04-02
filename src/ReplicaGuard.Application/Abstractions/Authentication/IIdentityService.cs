using Microsoft.AspNetCore.Identity;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Application.Abstractions.Authentication;

public interface IIdentityService
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<Result<IdentityUser>> CreateUserAsync(
        string username,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default);
    Task<IdentityUser?> FindByEmailAsync(string email);
    Task<bool> CheckPasswordAsync(IdentityUser identityUser, string password);
    Task<IList<string>> GetRolesAsync(IdentityUser identityUser);
}
