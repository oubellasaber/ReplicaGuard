using ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;
using ReplicaGuard.Domain.HosterAccounts;
using static ReplicaGuard.Api.Controllers.HosterAccounts.IdentityPayloadRequest;

namespace ReplicaGuard.Api.Controllers.HosterAccounts;

public static class IdentityMapper
{
    public static IdentityDto MapIdentity(IdentityPayloadRequest dto)
    {
        return dto switch
        {
            EmailIdentityRequest e => new IdentityDto(
                IdentityType.Email,
                e.Email,
                new Dictionary<SecretType, string>
                {
                { SecretType.Password, e.Password }
                }),

            UsernameIdentityRequest u => new IdentityDto(
                IdentityType.Username,
                u.Username,
                new Dictionary<SecretType, string>
                {
                { SecretType.Password, u.Password }
                }),

            ApiKeyIdentityRequest a => new IdentityDto(
                IdentityType.ApiKey,
                null,
                new Dictionary<SecretType, string>
                {
                { SecretType.ApiKeyPair, a.ApiKey }
                }),

            _ => throw new InvalidOperationException("Unknown identity payload type.")
        };
    }

}
