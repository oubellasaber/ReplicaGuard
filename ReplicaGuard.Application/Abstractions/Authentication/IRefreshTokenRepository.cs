using Microsoft.AspNetCore.Identity;

namespace ReplicaGuard.Application.Abstractions.Authentication;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct);
    void Add(string token, string identityUserId);
    void Update(RefreshToken refreshToken);
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Token { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public IdentityUser User { get; set; } = null!;
}
