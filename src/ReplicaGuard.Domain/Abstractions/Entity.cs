using System.Collections;
using System.Security.Cryptography;

namespace ReplicaGuard.Core.Abstractions;

public abstract class Entity<TEntityId> : IEntity, IEquatable<Entity<TEntityId>>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected Entity(TEntityId id)
    {
        Id = id;
    }

    // EF Migration usage
    //protected Entity() { }

    public TEntityId Id { get; init; }

    public bool Equals(Entity<TEntityId>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        if (Id is null)
            return false;

        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TEntityId>);
    
    public override int GetHashCode()
    {
        return Id is null
            ? base.GetHashCode()
            : HashCode.Combine(GetType(), Id);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    public IReadOnlyList<IDomainEvent> GetDomainEvents() => _domainEvents.ToList();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
