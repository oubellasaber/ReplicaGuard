using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ReplicaGuard.Application.Abstractions.Authentication;

namespace ReplicaGuard.Infrastructure.Authentication;

internal sealed class TokenProvider : ITokenProvider
{
    private readonly JwtAuthOptions _jwtAuthOptions;

    public TokenProvider(IOptions<JwtAuthOptions> jwtAuthOptions)
    {
        _jwtAuthOptions = jwtAuthOptions.Value;
    }

    public (string AccessToken, string RefreshToken) Create(
        string identityUserId,
        string email,
        IEnumerable<string> roles)
    {
        return (GenerateToken(identityUserId, email, roles), GenerateRefreshToken());
    }

    private string GenerateToken(
        string identityUserId,
        string email,
        IEnumerable<string> roles)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(_jwtAuthOptions.Key));
        SigningCredentials signingCredentials = new(securityKey, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, identityUserId),
            .. roles.Select(role => new Claim(JwtCustomClaimNames.Role, role)),
        ];

        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Issuer = _jwtAuthOptions.Issuer,
            Audience = _jwtAuthOptions.Audience,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = signingCredentials,
            Expires = DateTime.UtcNow.AddMinutes(_jwtAuthOptions.ExpirationInMinutes),
        };

        JsonWebTokenHandler handler = new();
        string accessToken = handler.CreateToken(tokenDescriptor);

        return accessToken;
    }

    private static string GenerateRefreshToken()
    {
        byte[] guidBytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String([.. guidBytes, .. randomBytes]);
    }
}
