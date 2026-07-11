namespace ReplicaGuard.Infrastructure.Cleanup;

public sealed class UserUploadsOptions
{
    public static readonly string SectionName = "UserUploads";
    public required string UploadDirectory { get; init; }
}
