using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Infrastructure.Hosters.SendCm.Upload;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal sealed class SendCmUploadSessionProvider
{
    private const string SessionIdPropertyName = "sess_id";
    private const string UploadServerPropertyName = "result";

    private readonly HttpClient _httpClient;
    private readonly SendCmOptions _options;

    public SendCmUploadSessionProvider(HttpClient httpClient, IOptions<SendCmOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<Result<UploadSessionContext>> GetSessionAsync(string apiKey, CancellationToken ct)
    {
        string url = QueryHelpers.AddQueryString(_options.UploadServerEndpoint, "key", apiKey);

        using var response = await _httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return Result.Failure<UploadSessionContext>(
                SendCmUploadErrors.HttpFailure(url, response.StatusCode));

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty(SessionIdPropertyName, out var sess))
            return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingSessionId());

        if (!doc.RootElement.TryGetProperty(UploadServerPropertyName, out var server))
            return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingUploadServer());

        var sessionId = sess.GetString();
        var uploadServer = server.GetString()?.Split('?')[0];

        if (string.IsNullOrWhiteSpace(sessionId))
            return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingSessionId());

        if (string.IsNullOrWhiteSpace(uploadServer))
            return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingUploadServer());

        return Result.Success(new UploadSessionContext(sessionId, uploadServer));
    }

    public readonly record struct UploadSessionContext(
        string SessionId,
        string UploadServer);
}
