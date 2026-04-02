using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ReplicaGuard.Infrastructure.Authentication;

internal static class ClaimsPrincipalExtensions
{
    public static string GetIdentityId(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
               throw new ApplicationException("User identity is unavailable");
    }
}
