using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;

public sealed class AddHosterCredentialsCommandHandler(
    IHosterCredentialsRepository credentials,
    IHosterRepository hosters,
    IUserContext userContext,
    IUnitOfWork uow,
    ILogger<AddHosterCredentialsCommand> logger)
        : ICommandHandler<AddHosterCredentialsCommand, AddHosterCredentialsResponse>
{
    private readonly IHosterCredentialsRepository _credentials = credentials;

    public async Task<Result<AddHosterCredentialsResponse>> Handle(
        AddHosterCredentialsCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate hoster exists
        var hoster = await hosters.GetByIdAsync(request.HosterId, cancellationToken);
        if (hoster == null)
            return Result.Failure<AddHosterCredentialsResponse>(HosterErrors.NotFound(request.HosterId));

        // 2. Check for existing credentials
        var userId = userContext.UserId;
        var existing = await _credentials.FindByUserAndHosterAsync(
            userId, request.HosterId, cancellationToken);
        if (existing != null)
            return Result.Failure<AddHosterCredentialsResponse>(
                HosterCredentialsErrors.AlreadyExists(hoster.Code));

        // 3. Create credentials
        var credentialsResult = hoster.CreateCredentials(
            userId,
            apiKey: request.ApiKey,
            email: request.Email,
            username: request.Username,
            password: request.Password);
        if (credentialsResult.IsFailure)
            return Result.Failure<AddHosterCredentialsResponse>(credentialsResult.Error);

        var credentials = credentialsResult.Value;

        // 4. Add and persist credentials
        _credentials.Add(credentials);
        await uow.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Successfully created credentials for user {UserId} on hoster {HosterId}",
            userId,
            request.HosterId);

        return Result.Success(new AddHosterCredentialsResponse(
            credentials.Id,
            credentials.HosterId,
            credentials.SyncStatus.ToString().ToLowerInvariant(),
            credentials.CreatedAtUtc));
    }
}
