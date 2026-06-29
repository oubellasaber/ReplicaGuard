using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Common;

public static class CommonErrors
{
    public static Error NotFound<T>(string entity, T id) =>
        new Error($"{entity}.NotFound", $"{entity} with id '{id}' was not found.")
        .WithType(ErrorType.NotFound);

    public static Error NotFound(string entity, Guid id) =>
        NotFound(entity, id.ToString());

    public static Error NotFound<T>(string entity, IEnumerable<T> ids) =>
    new Error($"{entity}.NotFound", $"No {entity} was found for ids '{string.Join(", ", ids)}'.")
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
