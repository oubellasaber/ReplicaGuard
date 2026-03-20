using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Value object representing a validated file name.
/// </summary>
public sealed record FileName
{
    public string Value { get; }

    private FileName(string value) => Value = value;

    public static Result<FileName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<FileName>(ReplicationErrors.FileNameEmpty);

        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (value.Any(c => invalidChars.Contains(c)))
            return Result.Failure<FileName>(ReplicationErrors.FileNameInvalidChars);

        if (value.Length > 255)
            return Result.Failure<FileName>(ReplicationErrors.FileNameTooLong(value.Length));

        return new FileName(value);
    }

    public override string ToString() => Value;

    public static implicit operator string(FileName fileName) => fileName.Value;
}
