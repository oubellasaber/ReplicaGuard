using ReplicaGuard.Core.Abstractions;
using System.IO;

namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Represents a local file path on the user's computer.
/// The file exists on the user's local filesystem and will be read directly from there.
/// </summary>
public sealed record LocalFileSource : FileSource
{
    /// <summary>
    /// Absolute or relative path to the file on the user's local filesystem.
    /// Example: "C:\Users\John\file.zip" or "/home/john/file.zip"
    /// </summary>
    public string FilePath { get; }

    public override bool IsRemote => false;
    public override bool IsLocal => true;

    private LocalFileSource(string filePath) : base(FileSourceType.Local)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Creates a local file source from a file path.
    /// </summary>
    public static Result<LocalFileSource> Create(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result.Failure<LocalFileSource>(
                ReplicationErrors.FilePathEmpty);

        // Normalize path separators for cross-platform compatibility
        string normalizedPath = filePath.Replace('\\', Path.DirectorySeparatorChar)
                                        .Replace('/', Path.DirectorySeparatorChar);

        return new LocalFileSource(normalizedPath);
    }

    /// <summary>
    /// Gets the file name from the path.
    /// </summary>
    public string GetFileName() => Path.GetFileName(FilePath);

    /// <summary>
    /// Checks if the file exists (infrastructure concern, but useful for validation).
    /// </summary>
    public bool FileExists() => File.Exists(FilePath);

    public override string ToString() => $"Local:{FilePath}";
}
