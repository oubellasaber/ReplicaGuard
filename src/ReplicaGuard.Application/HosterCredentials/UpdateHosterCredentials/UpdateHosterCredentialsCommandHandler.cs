using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.HosterCredentials.UpdateHosterCredentials;

public sealed class UpdateHosterCredentialsCommandHandler(
    IHosterCredentialsRepository credentials,
    IHosterRepository hosters,
    IUserContext userContext,
    IUnitOfWork uow,
    ILogger<UpdateHosterCredentialsCommandHandler> logger)
        : ICommandHandler<UpdateHosterCredentialsCommand>
{
    public async Task<Result> Handle(
        UpdateHosterCredentialsCommand request,
        CancellationToken cancellationToken)
    {
        Hoster? hoster = await hosters.GetByIdAsync(request.HosterId, cancellationToken);

        if (hoster is null)
            return Result.Failure(HosterErrors.NotFound(request.HosterId));

        var hosterCredentials = await credentials.FindByUserAndHosterAsync(
            userContext.UserId,
            request.HosterId,
            cancellationToken);

        if (hosterCredentials is null)
            return Result.Failure(HosterCredentialsErrors.NotFound(request.HosterId));

        Result updateResult = hoster.UpdateCredentials(
            hosterCredentials,
            apiKey: request.ApiKey,
            email: request.Email,
            username: request.Username,
            password: request.Password);

        if (updateResult.IsFailure)
            return updateResult;

        await uow.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Updated credentials {CredentialsId} for user {UserId} on hoster {HosterId}",
            hosterCredentials.Id,
            userContext.UserId,
            request.HosterId);

        return Result.Success();
    }
}
