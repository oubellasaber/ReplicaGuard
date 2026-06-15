namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal class SendCmOptions
{
    public static readonly string SectionName = "Hosters:SendCm";
    public required string ApiBaseUrl { get; init; }
    public required string UserInfoEndpoint { get; init; }
    public required string UploadServerEndpoint { get; init; }
    public required string RenameFileEndpoint { get; init; }
}
