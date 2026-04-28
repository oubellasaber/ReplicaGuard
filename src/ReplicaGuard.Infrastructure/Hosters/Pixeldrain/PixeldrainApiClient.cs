using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Hosters.Abstractions;
using PixeldrainHoster = ReplicaGuard.Core.Domain.Hoster.Pixeldrain;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain;

internal class PixeldrainApiClient(IHttpClientFactory clientFactory, IOptions<PixeldrainOptions> options) :
    HosterApiClientBase<PixeldrainHoster>, IValidateCredentials, IUploadFile
{
    private readonly HttpClient _httpClient = clientFactory.CreateClient(PixeldrainHoster.Code);
    private readonly HttpClient _uploadClient = clientFactory.CreateClient("FileUploadingHttpClient");
    private readonly PixeldrainOptions _options = options.Value;

    public async Task<Result<CredentialSet>> ValidateAsync(CredentialSet credentials, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            return Result.Failure<CredentialSet>(HosterCredentialsErrors.InvalidApiKey(Code));

        var isApiKeyValid = await IsApiKeyValidAsync(credentials.ApiKey, ct);

        return isApiKeyValid.IsFailure ? Result.Failure<CredentialSet>(isApiKeyValid.Error) : Result.Success(credentials);
    }

    public async Task<Result<UploadResponse>> UploadFromLocalStorageAsync(
        CredentialSet credentials,
        string fileName,
        FileStream fileStream,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNullOrEmpty(credentials.ApiKey);
        ArgumentNullException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(fileStream);

        try
        {
            using var content = new StreamContent(fileStream, 5 * 1024 * 1024);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var escapedFileName = Uri.EscapeDataString(fileName);
            var fileUploadUrl = $"{_options.FileUploadEndpoint.TrimEnd('/')}/{escapedFileName}";

            using var req = new HttpRequestMessage(HttpMethod.Put, fileUploadUrl)
            {
                Content = content,
                Headers = { Authorization = CreateAuthHeader(credentials.ApiKey) }
            };

            // Using the upload client that has a larger timeout configuration
            using var res = await _uploadClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!res.IsSuccessStatusCode)
            {
                return await HandleErrorResponseAsync(res, ct);
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("id", out var idElement))
            {
                return Result.Failure<UploadResponse>(
                    PixeldrainUploadErrors.UnknownError((int)res.StatusCode, "Missing 'id' in success response"));
            }

            var fileId = idElement.GetString();
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return Result.Failure<UploadResponse>(
                    PixeldrainUploadErrors.UnknownError((int)res.StatusCode, "Empty 'id' in success response"));
            }

            var fileUrl = new Uri($"{_options.FileUrlBase}/{fileId}");

            return Result.Success(new UploadResponse(
                FileId: fileId,
                FileUrl: fileUrl,
                FileName: fileName,
                SizeBytes: fileStream.Length,
                UploadedAt: DateTime.UtcNow
            ));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<UploadResponse>(
                HosterUploadErrors.UploadFailed(Code, $"Network error: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            return Result.Failure<UploadResponse>(
                HosterUploadErrors.UploadFailed(Code, $"Invalid JSON response: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Result.Failure<UploadResponse>(
                HosterUploadErrors.UploadFailed(Code, $"Unexpected error: {ex.Message}"));
        }
    }

    public Task<Result<UploadResponse>> UploadFromRemoteUrlAsync(
        CredentialSet credentials,
        string fileName,
        RemoteFileSource remoteUrl,
        CancellationToken ct = default)
    {
        return Task.FromResult(
            Result.Failure<UploadResponse>(
                HosterUploadErrors.UploadMethodNotSupported(
                    Code,
                    UploadMethod.RemoteUrl)));
    }

    public async Task<Result> IsApiKeyValidAsync(string apiKey, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Head, _options.UserInfoEndpoint);
        req.Headers.Authorization = CreateAuthHeader(apiKey);

        var res = await _httpClient.SendAsync(req, ct);

        return !res.IsSuccessStatusCode ? Result.Failure(HosterCredentialsErrors.InvalidApiKey(Code)) : Result.Success();
    }

    protected virtual AuthenticationHeaderValue CreateAuthHeader(string apiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(":" + apiKey);
        var encodedApiKey = Convert.ToBase64String(bytes);
        return new AuthenticationHeaderValue("Basic", encodedApiKey);
    }

    private async Task<Result<UploadResponse>> HandleErrorResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return Result.Failure<UploadResponse>(PixeldrainUploadErrors.NoFile());
        }

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge ||
            response.StatusCode == HttpStatusCode.InternalServerError)
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                if (doc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    var errorValue = valueElement.GetString();

                    return errorValue switch
                    {
                        "file_too_large" => Result.Failure<UploadResponse>(PixeldrainUploadErrors.FileTooLarge()),
                        "name_too_long" => Result.Failure<UploadResponse>(PixeldrainUploadErrors.NameTooLong()),
                        "writing" => Result.Failure<UploadResponse>(PixeldrainUploadErrors.WritingError()),
                        "internal" => Result.Failure<UploadResponse>(PixeldrainUploadErrors.InternalServerError()),
                        _ => Result.Failure<UploadResponse>(PixeldrainUploadErrors.UnknownError((int)response.StatusCode, errorValue ?? string.Empty))
                    };
                }
            }
            catch (JsonException ex)
            {
                return Result.Failure<UploadResponse>(PixeldrainUploadErrors.UnknownError((int)response.StatusCode, ex.Message));
            }
        }

        var errorContent = await response.Content.ReadAsStringAsync(ct);
        return Result.Failure<UploadResponse>(PixeldrainUploadErrors.UnknownError((int)response.StatusCode, errorContent));
    }
}
