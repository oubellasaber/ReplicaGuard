namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal class SendcmOptions
{
    public required string ApiBaseUrl { get; init; }
    public required string UserInfoEndpoint { get; init; }
    public required string UploadServerEndpoint { get; init; }
    public required string RenameFileEndpoint { get; init; }
}
