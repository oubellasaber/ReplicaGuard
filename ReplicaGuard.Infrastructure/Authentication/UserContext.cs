using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Core.Domain.User;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Authentication;

internal sealed class UserContext(ApplicationDbContext appDbContext, IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private Guid GetUserId()
    {
        var identityId = IdentityId;
        var user = appDbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefault(u => u.IdentityId == identityId);

        return user is null ? throw new ApplicationException("User not found in the database") : user.Id;
    }

    public Guid UserId => GetUserId();

    public string IdentityId =>
        httpContextAccessor
            .HttpContext?
            .User
            .GetIdentityId() ??
        throw new ApplicationException("User context is unavailable");
}
