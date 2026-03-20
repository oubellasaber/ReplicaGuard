using Microsoft.AspNetCore.Identity;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.User;

namespace ReplicaGuard.Application.Users.LogInUser;

public sealed class LogInUserCommandHandler(
    IIdentityService identityService,
    ITokenProvider tokenProvider,
    IIdentityUnitOfWork identityUnitOfWork,
    IRefreshTokenRepository refreshTokenRepository) : ICommandHandler<LogInUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(LogInUserCommand request, CancellationToken cancellationToken)
    {
        IdentityUser? identityUser = await identityService.FindByEmailAsync(request.Email);

        if (identityUser is null)
        {
            await Task.Delay(Random.Shared.Next(20, 30), cancellationToken); // timing attack mitigation
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        bool passwordValid = await identityService.CheckPasswordAsync(identityUser, request.Password);

        if (!passwordValid)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        var roles = await identityService.GetRolesAsync(identityUser);

        var accessTokens = tokenProvider.Create(identityUser.Id, request.Email, roles);

        refreshTokenRepository.Add(accessTokens.RefreshToken, identityUser.Id);

        await identityUnitOfWork.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessTokens.AccessToken, accessTokens.RefreshToken);
    }
}
