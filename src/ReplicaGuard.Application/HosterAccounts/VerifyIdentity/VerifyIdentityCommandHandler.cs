using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.HosterAccounts.VerifiyIdentity;

internal sealed class VerifiyIdentityCommandHandler(
    IHosterAccountRepository accounts,
    IHosterDefinitionResolver resolver,
    ICapabilityFactory factory,
    IUnitOfWork uow)
    : ICommandHandler<VerifyIdentityCommand>
{
    public async Task<Result> Handle(VerifyIdentityCommand request, CancellationToken ct)
    {
        var identityId = request.IdentityId;

        // 1. Load hoster account by identity ID
        var account = await accounts.GetByIdentityId(identityId, ct);
        if (account is null)
            return Result.Failure(AuthIdentityErrors.NotFound(identityId));

        // 2. Load identity
        var identity = account.Identities.Single(i => i.Id == identityId);

        // 3. Resolve hoster definition
        var def = resolver.Resolve(account.HosterCode);
        if (def is null)
            return Result.Failure(HosterErrors.NotFound(account.HosterCode));

        // 4. Resolve verification capability handler
        var handler = factory.Get<IIdentityVerificationHandler>(account.HosterCode);
        if (handler is null)
            return Result.Failure(IdentityVerificationErrors.NotSupported(account.HosterCode));
        
        // 5. Build verification request
        var verifyRequest = new IdentityVerificationRequest(identity);

        // 6. Execute verification
        var result = await handler.HandleAsync(verifyRequest, ct);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        // 7. Mark identity as verified
        identity.MarkAsVerified();

        // 8. Persist
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
