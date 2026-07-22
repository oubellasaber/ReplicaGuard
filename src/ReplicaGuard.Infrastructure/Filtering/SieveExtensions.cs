using System.Linq.Expressions;
using Sieve.Services;

namespace ReplicaGuard.Infrastructure.Filtering;

public static class SieveExtensions
{
    public static SievePropertyMapper Map<TEntity, TDto>(
        this SievePropertyMapper mapper,
        Expression<Func<TEntity, object?>> entityProperty,
        Expression<Func<TDto, object?>> dtoProperty,
        bool canFilter = true,
        bool canSort = true)
    {
        string publicName = GetPropertyName(dtoProperty);
        var fluent = mapper.Property<TEntity>(entityProperty!).HasName(publicName);

        if (canFilter) fluent.CanFilter();
        if (canSort) fluent.CanSort();

        return mapper;
    }

    private static string GetPropertyName<T>(Expression<Func<T, object?>> propertyExpression)
    {
        ArgumentNullException.ThrowIfNull(propertyExpression);

        var body = propertyExpression.Body;

        // 1. Unwrap Convert/Boxing unary node created for value types and long?
        if (body is UnaryExpression unary)
        {
            body = unary.Operand;
        }

        // 2. Extract MemberName safely
        if (body is MemberExpression member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException(
            $"Invalid expression format. Expected a member access expression, but got {body.NodeType}.",
            nameof(propertyExpression));
    }
}
