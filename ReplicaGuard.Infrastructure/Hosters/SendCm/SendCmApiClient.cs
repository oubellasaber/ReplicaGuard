using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Hosters.Abstractions;
using SendCmHoster = ReplicaGuard.Core.Domain.Hoster.SendCm;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal class SendCmApiClient : HosterApiClientBase<SendCmHoster>, IValidateCredentials, IUploadFile
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _uploadClient;
    private readonly SendcmOptions _options;
    private readonly ILogger<SendCmApiClient> _logger;

    public SendCmApiClient(
        IHttpClientFactory clientFactory,
        IOptions<SendcmOptions> options,
        ILogger<SendCmApiClient> logger)
    {
        _httpClient = clientFactory.CreateClient(Code);
        _uploadClient = clientFactory.CreateClient("FileUploadingHttpClient");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<CredentialSet>> ValidateAsync(CredentialSet credentials, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            _logger.LogWarning(
                "Credential validation failed for hoster {HosterCode}: missing API key",
                Code);

            return Result.Failure<CredentialSet>(HosterCredentialsErrors.InvalidApiKey(Code));
        }

        _logger.LogDebug(
            "Validating credentials for hoster {HosterCode}",
            Code);

        Result isApiKeyValid = await IsApiKeyValidAsync(credentials.ApiKey, ct);

        if (isApiKeyValid.IsFailure)
        {
            _logger.LogWarning(
                "Credential validation failed for hoster {HosterCode}: {ErrorCode}",
                Code,
                isApiKeyValid.Error.Code);

            return Result.Failure<CredentialSet>(isApiKeyValid.Error);
        }

        _logger.LogInformation(
            "Credential validation succeeded for hoster {HosterCode}",
            Code);

        return Result.Success(credentials);
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

        _logger.LogInformation(
            "Starting local upload to hoster {HosterCode} for file {FileName} ({Bytes} bytes)",
            Code,
            fileName,
            fileStream.Length);

        try
        {
            Result<string> sessionIdResult = await GetSessionIdAsync(credentials.ApiKey, ct);
            if (sessionIdResult.IsFailure)
            {
                _logger.LogWarning(
                    "Local upload to hoster {HosterCode} failed before upload start: could not resolve session ID",
                    Code);

                return Result.Failure<UploadResponse>(sessionIdResult.Error);
            }

            Result<string> uploadServerResult = await GetUploadServerAsync(credentials.ApiKey, ct);
            if (uploadServerResult.IsFailure)
            {
                _logger.LogWarning(
                    "Local upload to hoster {HosterCode} failed before upload start: could not resolve upload server",
                    Code);

                return Result.Failure<UploadResponse>(uploadServerResult.Error);
            }

            string uploadUrl = $"{uploadServerResult.Value}?upload_type=file&utype=reg";

            _logger.LogDebug(
                "Resolved upload server for hoster {HosterCode}: {UploadUrl}",
                Code,
                uploadUrl);

            using MultipartFormDataContent content = new()
            {
                { new StringContent(sessionIdResult.Value), "sess_id" },
                { new StringContent("reg"), "utype" },
                { new StringContent("1"), "file_public" },
                { new StringContent("", Encoding.UTF8), "link_pass" },
                { new StringContent(""), "to_folder" },
                { new StringContent("1"), "add_my_acc" },
                { new StringContent("1"), "keepalive" }
            };

            StreamContent fileContent = new(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file_0", fileName);

            // Execute the long-running upload via _uploadClient instead of _httpClient
            using var response = await _uploadClient.PostAsync(uploadUrl, content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Local upload to hoster {HosterCode} failed with status code {StatusCode}",
                    Code,
                    (int)response.StatusCode);

                return Result.Failure<UploadResponse>(HosterUploadErrors.UploadFailed(Code, UploadMethod.LocalStorage, response.StatusCode));
            }

            string body = await response.Content.ReadAsStringAsync(ct);

            Result<string> fileCodeResult = ExtractFileCode(body);
            if (fileCodeResult.IsFailure)
            {
                _logger.LogWarning(
                    "Local upload to hoster {HosterCode} returned an invalid file code: {ErrorCode}",
                    Code,
                    fileCodeResult.Error.Code);

                return Result.Failure<UploadResponse>(fileCodeResult.Error);
            }

            Result<UpdateStat> updateStatResult = ParseUpdateStat(body);
            if (updateStatResult.IsFailure)
            {
                _logger.LogWarning(
                    "Local upload to hoster {HosterCode} returned an invalid update_stat payload: {ErrorCode}",
                    Code,
                    updateStatResult.Error.Code);

                return Result.Failure<UploadResponse>(updateStatResult.Error);
            }

            string fileCode = fileCodeResult.Value;
            Uri fileUrl = new($"{_options.ApiBaseUrl}/{fileCode}");
            long sizeBytes = updateStatResult.Value.Total;

            _logger.LogInformation(
                "Local upload to hoster {HosterCode} succeeded for file {FileName}. FileCode={FileCode}, SizeBytes={SizeBytes}, Url={FileUrl}",
                Code,
                fileName,
                fileCode,
                sizeBytes,
                fileUrl);

            return Result.Success(new UploadResponse(fileCode, fileUrl, fileName, sizeBytes, DateTime.UtcNow));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Network error during local upload to hoster {HosterCode} for file {FileName}",
                Code,
                fileName);

            return Result.Failure<UploadResponse>(HosterUploadErrors.UploadFailed(Code, $"Network error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during local upload to hoster {HosterCode} for file {FileName}",
                Code,
                fileName);

            return Result.Failure<UploadResponse>(HosterUploadErrors.UploadFailed(Code, $"Unexpected error: {ex.Message}"));
        }
    }

    public async Task<Result<UploadResponse>> UploadFromRemoteUrlAsync(
        CredentialSet credentials,
        string fileName,
        RemoteFileSource remoteUrl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNullOrEmpty(credentials.ApiKey);
        ArgumentNullException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(remoteUrl);

        _logger.LogInformation(
            "Starting remote URL upload to hoster {HosterCode} for file {FileName} from host {RemoteHost}",
            Code,
            fileName,
            remoteUrl.Url.Value.Host);

        try
        {
            Result<string> sessionIdResult = await GetSessionIdAsync(credentials.ApiKey, ct);
            if (sessionIdResult.IsFailure)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} failed before upload start: could not resolve session ID",
                    Code);

                return Result.Failure<UploadResponse>(sessionIdResult.Error);
            }

            Result<string> uploadServerResult = await GetUploadServerAsync(credentials.ApiKey, ct);
            if (uploadServerResult.IsFailure)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} failed before upload start: could not resolve upload server",
                    Code);

                return Result.Failure<UploadResponse>(uploadServerResult.Error);
            }

            string uploadId = GenerateRandomUploadId();
            string uploadUrl = $"{uploadServerResult.Value}?upload_type=url&upload_id={uploadId}";

            _logger.LogDebug(
                "Resolved remote upload server for hoster {HosterCode}: {UploadUrl}, UploadId={UploadId}",
                Code,
                uploadUrl,
                uploadId);

            using MultipartFormDataContent content = new()
            {
                { new StringContent(sessionIdResult.Value), "sess_id" },
                { new StringContent("reg"), "utype" },
                { new StringContent("1"), "file_public" },
                { new StringContent(remoteUrl.Url.Value.ToString()), "url_mass" },
                { new StringContent("", Encoding.UTF8), "link_pass" },
                { new StringContent(""), "to_folder" },
                { new StringContent("1"), "add_my_acc" },
                { new StringContent("1"), "keepalive" }
            };

            using var response = await _httpClient.PostAsync(uploadUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} failed with status code {StatusCode}",
                    Code,
                    (int)response.StatusCode);

                return Result.Failure<UploadResponse>(
                    HosterUploadErrors.UploadFailed(Code, UploadMethod.RemoteUrl, response.StatusCode));
            }

            // Handle ("# Keep-Alive\n[{\"file_code\":\"undef\",\"file_status\":\"this file is banned by administrator\"}]")
            string body = await response.Content.ReadAsStringAsync(ct);

            Result<string> fileCodeResult = ExtractFileCode(body);
            if (fileCodeResult.IsFailure)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} returned an invalid file code: {ErrorCode}",
                    Code,
                    fileCodeResult.Error.Code);

                return Result.Failure<UploadResponse>(fileCodeResult.Error);
            }

            string updateStatUrl = $"{new Uri(uploadServerResult.Value).GetLeftPart(UriPartial.Authority)}/tmp/{uploadId}.json";
            using var updateStatResponse = await _httpClient.PostAsync(updateStatUrl, content, ct);
            string updateStatBody = await updateStatResponse.Content.ReadAsStringAsync(ct);

            Result<UpdateStat> updateStatResult = ParseUpdateStat(updateStatBody);
            if (updateStatResult.IsFailure)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} returned an invalid update_stat payload: {ErrorCode}",
                    Code,
                    updateStatResult.Error.Code);

                return Result.Failure<UploadResponse>(updateStatResult.Error);
            }

            string fileCode = fileCodeResult.Value;
            Uri fileUrl = new($"{_options.ApiBaseUrl}/{fileCode}");
            long sizeBytes = updateStatResult.Value.Total;

            _logger.LogInformation(
                "Remote URL upload to hoster {HosterCode} succeeded for file {FileName}. FileCode={FileCode}, SizeBytes={SizeBytes}, Url={FileUrl}",
                Code,
                fileName,
                fileCode,
                sizeBytes,
                fileUrl);

            return Result.Success(new UploadResponse(fileCode, fileUrl, fileName, sizeBytes, DateTime.UtcNow));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Network error during remote URL upload to hoster {HosterCode} for file {FileName}",
                Code,
                fileName);

            return Result.Failure<UploadResponse>(
                HosterUploadErrors.UploadFailed(Code, $"Network error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during remote URL upload to hoster {HosterCode} for file {FileName}",
                Code,
                fileName);

            return Result.Failure<UploadResponse>(
                HosterUploadErrors.UploadFailed(Code, $"Unexpected error: {ex.Message}"));
        }
            }

    private async Task<Result> IsApiKeyValidAsync(string apiKey, CancellationToken ct)
    {
        string url = QueryHelpers.AddQueryString(_options.UserInfoEndpoint, "key", apiKey);

        _logger.LogDebug(
            "Checking API key validity for hoster {HosterCode} against {Endpoint}",
            Code,
            _options.UserInfoEndpoint);

        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "API key validation failed for hoster {HosterCode} with status code {StatusCode}",
                Code,
                (int)response.StatusCode);

            return Result.Failure(HosterCredentialsErrors.InvalidApiKey(Code));
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("status", out JsonElement statusElement))
        {
            _logger.LogWarning(
                "API key validation failed for hoster {HosterCode}: missing status property",
                Code);

            return Result.Failure(HosterCredentialsErrors.InvalidApiKey(Code));
        }

        int status = statusElement.GetInt32();

        if (status == 403)
        {
            _logger.LogWarning(
                "API key validation failed for hoster {HosterCode}: provider returned status 403",
                Code);

            return Result.Failure(HosterCredentialsErrors.InvalidApiKey(Code));
        }

        _logger.LogDebug(
            "API key validation response for hoster {HosterCode} was successful",
            Code);

        return Result.Success();
    }

    private async Task<Result<string>> GetSessionIdAsync(string apiKey, CancellationToken ct)
    {
        try
        {
            string url = QueryHelpers.AddQueryString(_options.UploadServerEndpoint, "key", apiKey);

            _logger.LogDebug(
                "Requesting session ID for hoster {HosterCode}",
                Code);

            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to resolve session ID for hoster {HosterCode}. StatusCode={StatusCode}",
                    Code,
                    (int)response.StatusCode);

                return Result.Failure<string>(SendCmUploadErrors.MissingSessionId());
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("sess_id", out JsonElement sessIdProp))
            {
                _logger.LogWarning(
                    "Failed to resolve session ID for hoster {HosterCode}: response missing sess_id",
                    Code);

                return Result.Failure<string>(SendCmUploadErrors.MissingSessionId());
            }

            string? sessionId = sessIdProp.GetString();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                _logger.LogWarning(
                    "Failed to resolve session ID for hoster {HosterCode}: sess_id was empty",
                    Code);

                return Result.Failure<string>(SendCmUploadErrors.MissingSessionId());
            }

            _logger.LogDebug(
                "Resolved session ID for hoster {HosterCode}",
                Code);

            return Result.Success(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while resolving session ID for hoster {HosterCode}",
                Code);

            return Result.Failure<string>(SendCmUploadErrors.MissingSessionId());
        }
    }

    private async Task<Result<string>> GetUploadServerAsync(string apiKey, CancellationToken ct)
    {
        try
        {
            string url = QueryHelpers.AddQueryString(_options.UploadServerEndpoint, "key", apiKey);

            _logger.LogDebug(
                "Requesting upload server for hoster {HosterCode}",
                Code);

            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to resolve upload server for hoster {HosterCode}. StatusCode={StatusCode}",
                    Code,
                    (int)response.StatusCode);

                return Result.Failure<string>(SendCmUploadErrors.MissingUploadServer());
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("result", out JsonElement uploadServerProp))
            {
                _logger.LogWarning(
                    "Failed to resolve upload server for hoster {HosterCode}: response missing result",
                    Code);

                return Result.Failure<string>(SendCmUploadErrors.MissingUploadServer());
            }

            string? uploadServer = uploadServerProp.GetString()?.Split('?').First();
            if (string.IsNullOrWhiteSpace(uploadServer))
            {
                _logger.LogWarning(
                    "Failed to resolve upload server for hoster {HosterCode}: result was empty",
                    Code);

                return Result.Failure<string>(SendCmUploadErrors.MissingUploadServer());
            }

            _logger.LogDebug(
                "Resolved upload server for hoster {HosterCode}: {UploadServer}",
                Code,
                uploadServer);

            return Result.Success(uploadServer);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while resolving upload server for hoster {HosterCode}",
                Code);

            return Result.Failure<string>(SendCmUploadErrors.MissingUploadServer());
        }
    }

    private static Result<string> ExtractFileCode(string response)
    {
        try
        {
            int lastNewline = response.LastIndexOf('\n');
            string json = lastNewline >= 0 ? response[(lastNewline + 1)..].Trim() : response.Trim();

            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse("Expected an array."));

            if (doc.RootElement.GetArrayLength() == 0)
                return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse("Empty array returned."));

            if (!doc.RootElement[0].TryGetProperty("file_code", out JsonElement fileCodeProp))
                return Result.Failure<string>(SendCmUploadErrors.EmptyFileCode());

            string? fileCode = fileCodeProp.GetString();
            return string.IsNullOrWhiteSpace(fileCode)
                ? Result.Failure<string>(SendCmUploadErrors.EmptyFileCode())
                : Result.Success(fileCode);
        }
        catch (JsonException ex)
        {
            return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse(ex.Message));
        }
    }

    private static Result<UpdateStat> ParseUpdateStat(string input)
    {
        try
        {
            return Result.Success(UpdateStatParser.Parse(input));
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateStat>(SendCmUploadErrors.InvalidUpdateStatFormat()
                .WithDetail(ex.Message));
        }
    }

    private static string GenerateRandomUploadId()
    {
        Random random = new();
        return string.Concat(Enumerable.Range(0, 12).Select(_ => random.Next(10)));
    }

    public readonly record struct UpdateStat(
        long Loaded,
        int Pid,
        long Total,
        int FilesDone,
        string State);

    public static class UpdateStatParser
    {
        private const string Prefix = "update_stat(";

        public static UpdateStat Parse(string input)
        {
            // Plan:
            // 1. Trim the incoming payload and validate the expected update_stat(...) wrapper.
            // 2. Extract the JSON object inside the wrapper.
            // 3. Parse each field while accepting either JSON numbers or quoted numeric strings.
            // 4. Throw a clear format exception when a required field is missing or invalid.
            if (input is null)
                throw new ArgumentNullException(nameof(input));

            ReadOnlySpan<char> span = input.AsSpan().Trim();

            if (span.IsEmpty || !span.StartsWith(Prefix, StringComparison.Ordinal) || span[^1] != ')')
                throw new FormatException("Expected format: update_stat({...})");

            int startInOriginal = input.AsSpan().IndexOf(span);
            ReadOnlyMemory<char> trimmed = input.AsMemory(startInOriginal, span.Length);
            ReadOnlyMemory<char> json = trimmed.Slice(Prefix.Length, trimmed.Length - Prefix.Length - 1);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            return new UpdateStat(
                Loaded: GetRequiredInt64(root, "loaded"),
                Pid: GetRequiredInt32(root, "pid"),
                Total: GetRequiredInt64(root, "total"),
                FilesDone: GetRequiredInt32(root, "files_done"),
                State: GetRequiredString(root, "state"));
        }

        private static long GetRequiredInt64(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property))
                throw new FormatException($"Missing required property '{propertyName}'.");

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long numberValue))
                return numberValue;

            if (property.ValueKind == JsonValueKind.String
                && long.TryParse(
                    property.GetString(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long stringValue))
                return stringValue;

            throw new FormatException($"Property '{propertyName}' must be an integer or a numeric string.");
        }

        private static int GetRequiredInt32(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property))
                throw new FormatException($"Missing required property '{propertyName}'.");

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int numberValue))
                return numberValue;

            if (property.ValueKind == JsonValueKind.String
                && int.TryParse(
                    property.GetString(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int stringValue))
                return stringValue;

            throw new FormatException($"Property '{propertyName}' must be an integer or a numeric string.");
        }

        private static string GetRequiredString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property))
                throw new FormatException($"Missing required property '{propertyName}'.");

            string? value = property.GetString();
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException($"Property '{propertyName}' must be a non-empty string.");

            return value;
        }
    }
}
