namespace ReplicaGuard.Domain.HosterAccounts;

public abstract record IdentityPayload
{
    private IdentityPayload() { }

    public sealed record EmailPayload(
        string Email, 
        string Password) : IdentityPayload;

    public sealed record UsernamePayload(
        string Username, 
        string Password) : IdentityPayload;

    public sealed record ApiKeyPayload(
        string ApiKey) : IdentityPayload;
}
