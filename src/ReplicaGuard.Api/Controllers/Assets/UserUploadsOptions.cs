namespace ReplicaGuard.Api.Controllers.Assets;

public sealed class UserUploadsOptions
{
    public static readonly string SectionName = "UserUploads";
    public required string UploadDirectory { get; init; }
}
