using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Infrastructure.Authentication;
using ReplicaGuard.Infrastructure.Identity;

namespace ReplicaGuard.Infrastructure.Repositories;

internal class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppIdentityDbContext _db;
    private readonly JwtAuthOptions _jwtAuthOptions;

    public RefreshTokenRepository(
        AppIdentityDbContext db,
        IOptions<JwtAuthOptions> jwtAuthOptions)
    {
        _db = db;
        _jwtAuthOptions = jwtAuthOptions.Value;
    }

    public void Add(string token, string identityUserId)
    {
        RefreshToken refreshToken = new()
        {
            Id = Guid.NewGuid(),
            UserId = identityUserId,
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationInDays),
        };

        _db.RefreshTokens.Add(refreshToken);
    }

    public async Task<RefreshToken?> GetByTokenAsync(
       string token,
       CancellationToken cancellationToken)
    {
        return await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
    }

    public void Update(RefreshToken refreshToken)
    {
        _db.RefreshTokens.Update(refreshToken);
    }
}
