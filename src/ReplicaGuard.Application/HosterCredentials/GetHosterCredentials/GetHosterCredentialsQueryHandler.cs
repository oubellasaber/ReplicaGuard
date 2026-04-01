using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;

namespace ReplicaGuard.Application.HosterCredentials.GetHosterCredentials;

public sealed class GetHosterCredentialsQueryHandler(
    IHosterCredentialsRepository credentials,
    IUserContext userContext)
        : IQueryHandler<GetHosterCredentialsQuery, GetHosterCredentialsResponse>
{
    private const int MinLengthForPartialMask = 10;

    public async Task<Result<GetHosterCredentialsResponse>> Handle(
        GetHosterCredentialsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userContext.UserId;

        var hosterCredentials = await credentials.FindByUserAndHosterAsync(
            userId, request.HosterId, cancellationToken);

        if (hosterCredentials is null)
        {
            return Result.Failure<GetHosterCredentialsResponse>(
                HosterCredentialsErrors.NotFound(request.HosterId));
        }

        return Result.Success(new GetHosterCredentialsResponse(
            hosterCredentials.Id,
            hosterCredentials.HosterId,
            MaskSecret(hosterCredentials.ApiKey),
            hosterCredentials.Username,
            hosterCredentials.Email,
            MaskSecret(hosterCredentials.Password),
            hosterCredentials.SyncStatus.ToString().ToLowerInvariant(),
            hosterCredentials.CreatedAtUtc,
            hosterCredentials.UpdatedAtUtc));
    }

    private static string? MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
            return null;

        // Fully mask short secrets to avoid revealing too much
        if (secret.Length < MinLengthForPartialMask)
            return new string('*', secret.Length);

        // Show first 2 and last 2 characters for longer secrets
        return string.Concat(
            secret.AsSpan(0, 2),
            new string('*', secret.Length - 4),
            secret.AsSpan(secret.Length - 2));
    }
}
