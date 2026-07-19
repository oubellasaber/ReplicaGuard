using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Replication;

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
    public static Result<LocalFileSource> Create(string baseDirectory, string userSubmittedPath)
    {
        if (string.IsNullOrWhiteSpace(userSubmittedPath))
            return Result.Failure<LocalFileSource>(ReplicationErrors.FilePathEmpty);

        try
        {
            // 1. Get absolute, fully resolved path of the base directory sandbox
            string absoluteBase = Path.GetFullPath(baseDirectory);

            // Ensure it ends with a directory separator so "/app/spool-hacker" doesn't bypass "/app/spool"
            if (!absoluteBase.EndsWith(Path.DirectorySeparatorChar))
            {
                absoluteBase += Path.DirectorySeparatorChar;
            }

            // 2. Combine and resolve the full path (this evaluates and flattens all ".." parts)
            string combinedPath = Path.Combine(absoluteBase, userSubmittedPath);
            string absoluteFinalPath = Path.GetFullPath(combinedPath);

            // 3. Security Boundary Check: Does the final path still live inside the base sandbox?
            if (!absoluteFinalPath.StartsWith(absoluteBase, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<LocalFileSource>(ReplicationErrors.PathTraversalAttempted);
            }

            // 4. Basic character sanitization
            if (absoluteFinalPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return Result.Failure<LocalFileSource>(ReplicationErrors.InvalidPathCharacters);

            return new LocalFileSource(absoluteFinalPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result.Failure<LocalFileSource>(ReplicationErrors.MalformedFilePath);
        }
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
