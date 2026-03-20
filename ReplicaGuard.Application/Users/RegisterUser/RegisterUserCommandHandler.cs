using Microsoft.AspNetCore.Identity;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.User;

namespace ReplicaGuard.Application.Users.RegisterUser;

public class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IIdentityService identityService,
    ITokenProvider tokenProvider,
    IRefreshTokenRepository refreshTokenRepository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<RegisterUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        // Check if email is taken
        if (await identityService.EmailExistsAsync(request.Email, cancellationToken))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.EmailAlreadyTaken(request.Email));
        }

        // Check if username is taken
        if (await identityService.UsernameExistsAsync(request.Name, cancellationToken))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.UsernameAlreadyTaken(request.Name));
        }

        // Begin transaction across BOTH contexts
        await unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);

        try
        {
            // Create identity user
            Result<IdentityUser> identityResult = await identityService.CreateUserAsync(
                request.Name,
                request.Email,
                request.Password,
                Roles.Member,
                cancellationToken);

            if (identityResult.IsFailure)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<AccessTokensResponse>(identityResult.Error);
            }

            var identityUser = identityResult.Value;

            // Create domain user
            User user = User.Create(
                identityUser.Id,
                request.Email,
                request.Name,
                DateTime.UtcNow);

            userRepository.Add(user);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var accessTokens = tokenProvider.Create(identityUser.Id, identityUser.Email!, [Roles.Member]);

            // Store refresh token
            refreshTokenRepository.Add(accessTokens.RefreshToken, identityUser.Id);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return new AccessTokensResponse(accessTokens.AccessToken, accessTokens.RefreshToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
