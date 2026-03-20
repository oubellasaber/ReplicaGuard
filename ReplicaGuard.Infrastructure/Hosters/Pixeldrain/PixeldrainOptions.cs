namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain;

internal class PixeldrainOptions
{
    public required string ApiBaseUrl { get; init; }
    public required string UserInfoEndpoint { get; init; }
    public required string FileUploadEndpoint { get; init; }
    public required string FileUrlBase { get; init; }
}
