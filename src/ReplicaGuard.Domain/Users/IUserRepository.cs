using ReplicaGuard.Domain.Users;

public interface IUserRepository
{
    void Add(User user);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
