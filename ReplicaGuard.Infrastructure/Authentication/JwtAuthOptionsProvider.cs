using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Abstractions.Authentication;

namespace ReplicaGuard.Infrastructure.Authentication;

internal class JwtAuthOptionsProvider : IJwtAuthOptionsProvider
{
    private readonly JwtAuthOptions _options;

    public JwtAuthOptionsProvider(IOptions<JwtAuthOptions> options)
    {
        _options = options.Value;
    }

    public string Key => _options.Key;

    public string Issuer => _options.Issuer;

    public string Audience => _options.Audience;

    public int ExpirationInMinutes => _options.ExpirationInMinutes;

    public int RefreshTokenExpirationInDays => _options.RefreshTokenExpirationInDays;
}
