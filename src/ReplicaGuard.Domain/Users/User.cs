using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Users;
public class User : Entity<Guid>
{
    public string IdentityId { get; private set; }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }

    private User(
        string identityId,
        string email,
        string name,
        DateTime createdAtUtc) 
        : base(Guid.NewGuid())
    {
        IdentityId = identityId;
        Email = email;
        Name = name;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static User Create(string identityId, string email, string name, DateTime createdAtUtc)
    {
        return new User(identityId, email, name, createdAtUtc);
    }
}
