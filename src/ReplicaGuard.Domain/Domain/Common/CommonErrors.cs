using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Common;

public static class CommonErrors
{
    public static Error NotFound(string entity, Guid id) =>
        new Error($"{entity}.NotFound", $"{entity} with id '{id}' was not found.")
        .WithType(ErrorType.NotFound);

    public static Error NotFound(string entity, string field, string value) =>
        new Error($"{entity}.NotFound", $"{entity} with {field} '{value}' was not found.")
        .WithType(ErrorType.NotFound);

    public static Error AlreadyExists(string entity, string field, string value) =>
        new Error($"{entity}.AlreadyExists", $"{entity} with {field} '{value}' already exists.")
        .WithType(ErrorType.Conflict);

    public static Error Validation(string code, string message) =>
        new Error(code, message)
        .WithType(ErrorType.Validation);
}
