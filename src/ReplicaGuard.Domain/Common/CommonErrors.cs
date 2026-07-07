using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Common;

public static class CommonErrors
{
    public static Error NotFound<T>(string entity, T id) where T : notnull =>
        new Error($"{entity}.NotFound", $"{entity} with the specified id was not found.")
        .WithType(ErrorType.NotFound)
        .WithMetadata($"{entity}Id", id);

    public static Error NotFound(string entity, Guid id) =>
        NotFound<Guid>(entity, id);

    public static Error NotFound(string entity, string field, string value) =>
        new Error($"{entity}.NotFound", $"{entity} with the specified ${field} was not found.")
        .WithType(ErrorType.NotFound)
        .WithMetadata(field, value);

    //public static Error AlreadyExists(string entity, string field, string value) =>
    //    new Error($"{entity}.AlreadyExists", $"{entity} with {field} '{value}' already exists.")
    //    .WithType(ErrorType.Conflict);
}
