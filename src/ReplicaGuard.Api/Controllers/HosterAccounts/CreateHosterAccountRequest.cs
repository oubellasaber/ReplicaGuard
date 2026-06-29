using System.Text.Json.Serialization;

namespace ReplicaGuard.Api.Controllers.HosterAccounts;

public sealed record CreateHosterAccountRequest(
    Guid HosterId,
    string Alias,
    string? Description,
    List<IdentityPayloadRequest> Identities);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EmailIdentityRequest), "email")]
[JsonDerivedType(typeof(UsernameIdentityRequest), "username")]
[JsonDerivedType(typeof(ApiKeyIdentityRequest), "apikey")]
public abstract record IdentityPayloadRequest
{
    protected IdentityPayloadRequest() { }

    public sealed record EmailIdentityRequest(
        string Email,
        string Password) : IdentityPayloadRequest;

    public sealed record UsernameIdentityRequest(
        string Username,
        string Password) : IdentityPayloadRequest;

    public sealed record ApiKeyIdentityRequest(
        string ApiKey) : IdentityPayloadRequest;
}
