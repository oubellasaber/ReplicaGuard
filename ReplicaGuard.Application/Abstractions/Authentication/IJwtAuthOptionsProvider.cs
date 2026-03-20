namespace ReplicaGuard.Application.Abstractions.Authentication;

public interface IJwtAuthOptionsProvider
{
    string Key { get; }
    string Issuer { get; }
    string Audience { get; }
    int ExpirationInMinutes { get; }
    int RefreshTokenExpirationInDays { get; }
}
