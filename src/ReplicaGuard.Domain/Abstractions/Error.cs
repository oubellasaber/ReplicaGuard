using System.Reflection.Metadata.Ecma335;

namespace ReplicaGuard.Core.Abstractions;

public sealed record Error
{
    public static readonly Error NullValue = new("Error.NullValue", "Null value was provided");
    public static Error None { get; } = new("", "", false);

    public string Code { get; }
    public string Message { get; }
    public string? Detail { get; init; }
    public ErrorType Type { get; init; } = ErrorType.Failure;

    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();

    public Error(string code, string message, ErrorType type = ErrorType.InvalidInput)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Error code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message cannot be empty.", nameof(message));

        Code = code;
        Message = message;
        Type = type;
    }

    // Private constructor to bypass validation
    private Error(string code, string message, bool _)
    {
        Code = code;
        Message = message;
    }

    // Fluent API for additional context
    public Error WithDetail(string detail) => this with { Detail = detail };
    public Error WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }
    public Error WithType(ErrorType type) => this with { Type = type };
    public Error AsPermanent() => this with { Type = ErrorType.Permanent };
    public bool IsPermanent() => Type == ErrorType.Permanent;
}
