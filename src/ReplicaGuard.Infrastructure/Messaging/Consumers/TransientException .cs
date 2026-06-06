using ReplicaGuard.Core.Abstractions;

public sealed class TransientException : Exception
{
    public Error Error { get; }

    public TransientException(Error error)
        : base(BuildMessage(error))
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    private static string BuildMessage(Error error)
    {
        var meta = error.Metadata is { Count: > 0 }
            ? string.Join(", ", error.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"))
            : "None";

        return $"Code={error.Code}, Message={error.Message}, Detail={error.Detail ?? "None"}, Type={error.Type}, Kind={error.MessagingKind}, Metadata={meta}";
    }
}
