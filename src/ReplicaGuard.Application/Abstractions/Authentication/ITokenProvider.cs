namespace ReplicaGuard.Application.Abstractions.Authentication;

public interface ITokenProvider
{
    (string AccessToken, string RefreshToken) Create(
        string identityUserId,
        string email,
        IEnumerable<string> roles);
}
