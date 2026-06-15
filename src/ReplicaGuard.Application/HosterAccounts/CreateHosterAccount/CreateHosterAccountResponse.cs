namespace ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;

public sealed record CreateHosterAccountResponse(
    Guid HosterAccountId,
    string Alias,
    int TotalIdentities
);
