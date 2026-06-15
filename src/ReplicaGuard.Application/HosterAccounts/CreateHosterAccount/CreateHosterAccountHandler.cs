using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;

public sealed class CreateHosterAccountHandler : ICommandHandler<CreateHosterAccountCommand, CreateHosterAccountResponse>
{
    private readonly IHosterDefinitionResolver _resolver;
    private readonly IUserContext _userContext;
    private readonly IHosterRepository _hosters;
    private readonly IHosterAccountRepository _accounts;
    private readonly ISecretEncryptionService _crypto;
    private readonly IUnitOfWork _uow;

    public CreateHosterAccountHandler(
        IHosterDefinitionResolver resolver,
        IUserContext userContext,
        IHosterRepository hosters,
        IHosterAccountRepository accounts,
        ISecretEncryptionService crypto,
        IUnitOfWork uow)
    {
        _resolver = resolver;
        _userContext = userContext;
        _hosters = hosters;
        _accounts = accounts;
        _crypto = crypto;
        _uow = uow;
    }

    public async Task<Result<CreateHosterAccountResponse>> Handle(CreateHosterAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContext.UserId;
        var hosterId = request.Id;

        var hoster = await _hosters.GetByIdAsync(hosterId, cancellationToken);
        if (hoster is null)
            return Result.Failure<CreateHosterAccountResponse>(HosterErrors.NotFound(hosterId));

        var accountCreationResult = HosterAccount.Create(
            _resolver.Resolve(hoster.Id),
            userId,
            request.Alias,
            request.Description,
            request.Identities.Select(ToPayload),
            _crypto
        );

        if (accountCreationResult.IsFailure)
            return Result.Failure<CreateHosterAccountResponse>(accountCreationResult.Error);

        var account = accountCreationResult.Value;
        _accounts.Add(account);
        await _uow.SaveChangesAsync(cancellationToken);

        return new CreateHosterAccountResponse(
            account.Id,
            account.Alias,
            account.Identities.Count
    );
    }

    private static IdentityPayload ToPayload(IdentityDto dto)
    {
        return dto.Type.ToString().ToLowerInvariant() switch
        {
            "email" => new IdentityPayload.EmailPayload(
                dto.Value ?? throw new InvalidOperationException("Email required"),
                dto.PlaintextSecrets.GetValueOrDefault(SecretType.Password)!
            ),

            "username" => new IdentityPayload.UsernamePayload(
                dto.Value ?? throw new InvalidOperationException("Username required"),
                dto.PlaintextSecrets.GetValueOrDefault(SecretType.Password)!
            ),

            "apikey" => new IdentityPayload.ApiKeyPayload(
                dto.PlaintextSecrets.GetValueOrDefault(SecretType.ApiKeyPair)!
            ),

            _ => throw new InvalidOperationException($"Unknown identity type: {dto.Type}")
        };
    }

}
