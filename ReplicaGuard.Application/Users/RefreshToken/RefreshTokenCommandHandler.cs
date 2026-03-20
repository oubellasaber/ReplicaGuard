using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Application.Users.RefreshToken;

public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    private readonly IIdentityService _identityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITokenProvider _tokenProvider;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtAuthOptionsProvider _jwtAuthOptionsProvider;
    private readonly IIdentityUnitOfWork _unitOfWork;


    public RefreshTokenCommandHandler(
            IIdentityService identityService,
            IDateTimeProvider dateTimeProvider,
            ITokenProvider tokenProvider,
            IRefreshTokenRepository refreshTokenRepository,
            IJwtAuthOptionsProvider jwtOptionsProvider,
            IIdentityUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _dateTimeProvider = dateTimeProvider;
        _tokenProvider = tokenProvider;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtAuthOptionsProvider = jwtOptionsProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AccessTokensResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.refreshToken, cancellationToken);

        if (refreshToken is null || refreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Result.Failure<AccessTokensResponse>(AuthenticationErrors.InvalidRefreshToken);
        }

        IList<string> roles = await _identityService.GetRolesAsync(refreshToken.User);

        var accessTokens = _tokenProvider.Create(refreshToken.UserId, refreshToken.User.Email!, [.. roles]);

        refreshToken.Token = accessTokens.RefreshToken;
        refreshToken.ExpiresAtUtc = _dateTimeProvider.UtcNow.AddMinutes(_jwtAuthOptionsProvider.RefreshTokenExpirationInDays);

        _refreshTokenRepository.Update(refreshToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessTokens.AccessToken, accessTokens.RefreshToken);
    }
}
