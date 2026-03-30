using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Capabilities.Rename;
using ReplicaGuard.Core.Capabilities.Upload;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.Infrastructure.Hosters.Abstractions;
using SendCmHoster = ReplicaGuard.Core.Domain.Hoster.SendCm;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal class SendCmApiClient : HosterApiClientBase<SendCmHoster>, IValidateCredentials, IUploadFile, IRenameFile
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

        var sizeBytes = fileStream.Length;

        _logger.LogInformation(
            "Starting local upload to hoster {HosterCode} for file {FileName} ({Bytes} bytes)",
            Code,
            fileName,
            sizeBytes);

        try
        {
            Result<UploadSessionContext> uploadSessionResult = await GetUploadSessionContextAsync(credentials.ApiKey, ct);
            if (uploadSessionResult.IsFailure)
            {
                _logger.LogWarning(
                    "Local upload to hoster {HosterCode} failed before upload start: could not resolve upload session context",
                    Code);

                return Result.Failure<UploadResponse>(uploadSessionResult.Error);
            }

            UploadSessionContext uploadSession = uploadSessionResult.Value;

            string uploadUrl = $"{uploadSession.UploadServer}?upload_type=file&utype=reg";

            _logger.LogDebug(
                "Resolved upload server for hoster {HosterCode}: {UploadUrl}",
                Code,
                uploadUrl);

            using MultipartFormDataContent content = new()
            {
                { new StringContent("null"), "relativePath" },
                { new StringContent(fileName), "name" },
                { new StringContent("text/plain"), "type" },
                { new StringContent(""), "file_expire_unit" },
                { new StringContent(""), "file_max_dl" },
                { new StringContent(""), "link_rcpt" },
                { new StringContent(""), "file_expire_time" },
                { new StringContent("0"), "to_folder" },
                { new StringContent("", Encoding.UTF8), "link_pass" },
                { new StringContent(""), "file_public" },
                { new StringContent(uploadSession.SessionId), "sess_id" },
                { new StringContent("reg"), "utype" }
            };

            StreamContent fileContent = new(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"my_file\"",
                FileName = $"\"{Path.GetFileName(fileName)}\""
            };

            content.Add(fileContent);

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

            string fileCode = fileCodeResult.Value;
            Uri fileUrl = new($"{_options.ApiBaseUrl}/{fileCode}");

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
            Result<UploadSessionContext> uploadSessionResult = await GetUploadSessionContextAsync(credentials.ApiKey, ct);
            if (uploadSessionResult.IsFailure)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} failed before upload start: could not resolve upload session context",
                    Code);

                return Result.Failure<UploadResponse>(uploadSessionResult.Error);
            }

            UploadSessionContext uploadSession = uploadSessionResult.Value;

            string uploadId = GenerateRandomUploadId();
            string uploadUrl = $"{uploadSession.UploadServer}?upload_type=url&upload_id={uploadId}";

            _logger.LogDebug(
                "Resolved remote upload server for hoster {HosterCode}: {UploadUrl}, UploadId={UploadId}",
                Code,
                uploadUrl,
                uploadId);

            using MultipartFormDataContent content = new()
            {
                { new StringContent(uploadSession.SessionId), "sess_id" },
                { new StringContent("reg"), "utype" },
                { new StringContent("1"), "file_public" },
                { new StringContent(fileName), "name" },
                { new StringContent(remoteUrl.Url.Value.ToString()), "url_mass" },
                { new StringContent("", Encoding.UTF8), "link_pass" },
                { new StringContent(""), "to_folder" },
                { new StringContent("1"), "add_my_acc" },
                { new StringContent("1"), "keepalive" }
            };

            using HttpResponseMessage response = await _httpClient.PostAsync(uploadUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} failed with status code {StatusCode}",
                    Code,
                    (int)response.StatusCode);

                return Result.Failure<UploadResponse>(
                    HosterUploadErrors.UploadFailed(Code, UploadMethod.RemoteUrl, response.StatusCode));
            }

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

            string updateStatUrl = $"{new Uri(uploadSession.UploadServer).GetLeftPart(UriPartial.Authority)}/tmp/{uploadId}.json";
            using HttpResponseMessage updateStatResponse = await _httpClient.GetAsync(updateStatUrl, ct);
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

            Result renameResult = await RenameFileAsync(credentials, fileCode, fileName, ct);
            if (renameResult.IsFailure)
            {
                _logger.LogWarning(
                    "Remote URL upload to hoster {HosterCode} could not enforce filename via rename. FileCode={FileCode}, ErrorCode={ErrorCode}",
                    Code,
                    fileCode,
                    renameResult.Error.Code);

                return Result.Failure<UploadResponse>(renameResult.Error);
            }

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

    public async Task<Result> RenameFileAsync(
        CredentialSet credentials,
        string fileCode,
        string newFileName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNullOrEmpty(credentials.ApiKey);
        ArgumentNullException.ThrowIfNullOrEmpty(fileCode);
        ArgumentNullException.ThrowIfNullOrEmpty(newFileName);

        _logger.LogInformation(
            "Starting rename on hoster {HosterCode}. FileCode={FileCode}, NewName={NewName}",
            Code,
            fileCode,
            newFileName);

        try
        {
            Dictionary<string, string?> query = new()
            {
                ["key"] = credentials.ApiKey,
                ["file_code"] = fileCode,
                ["name"] = newFileName
            };

            string url = QueryHelpers.AddQueryString(_options.RenameFileEndpoint, query);

            using HttpResponseMessage response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Rename failed on hoster {HosterCode}. FileCode={FileCode}, StatusCode={StatusCode}",
                    Code,
                    fileCode,
                    (int)response.StatusCode);

                return response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => Result.Failure(SendCmFileErrors.RenameBadRequest()),
                    System.Net.HttpStatusCode.Forbidden => Result.Failure(SendCmFileErrors.RenameForbidden()),
                    System.Net.HttpStatusCode.NotFound => Result.Failure(SendCmFileErrors.RenameFileNotFound(fileCode)),
                    (System.Net.HttpStatusCode)451 => Result.Failure(SendCmFileErrors.RenameUnavailable()),
                    _ => Result.Failure(SendCmFileErrors.RenameFailed(
                        $"HTTP {(int)response.StatusCode} {response.StatusCode}"))
                };
            }

            string body = await response.Content.ReadAsStringAsync(ct);
            Result parseResult = ParseRenameResponse(body);

            if (parseResult.IsFailure)
            {
                _logger.LogWarning(
                    "Rename response invalid on hoster {HosterCode}. FileCode={FileCode}, ErrorCode={ErrorCode}",
                    Code,
                    fileCode,
                    parseResult.Error.Code);

                return parseResult;
            }

            _logger.LogInformation(
                "Rename succeeded on hoster {HosterCode}. FileCode={FileCode}, NewName={NewName}",
                Code,
                fileCode,
                newFileName);

            return Result.Success();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Network error during rename on hoster {HosterCode}. FileCode={FileCode}",
                Code,
                fileCode);

            return Result.Failure(SendCmFileErrors.RenameFailed($"Network error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during rename on hoster {HosterCode}. FileCode={FileCode}",
                Code,
                fileCode);

            return Result.Failure(SendCmFileErrors.RenameFailed($"Unexpected error: {ex.Message}"));
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

    private readonly record struct UploadSessionContext(
        string SessionId,
        string UploadServer);

    private async Task<Result<UploadSessionContext>> GetUploadSessionContextAsync(string apiKey, CancellationToken ct)
    {
        try
        {
            string url = QueryHelpers.AddQueryString(_options.UploadServerEndpoint, "key", apiKey);

            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingUploadServer());

            await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("sess_id", out JsonElement sessIdProp))
                return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingSessionId());

            if (!doc.RootElement.TryGetProperty("result", out JsonElement uploadServerProp))
                return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingUploadServer());

            string? sessionId = sessIdProp.GetString();
            string? uploadServer = uploadServerProp.GetString()?.Split('?').FirstOrDefault();

            if (string.IsNullOrWhiteSpace(sessionId))
                return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingSessionId());

            if (string.IsNullOrWhiteSpace(uploadServer))
                return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingUploadServer());

            return Result.Success(new UploadSessionContext(sessionId, uploadServer));
        }
        catch
        {
            return Result.Failure<UploadSessionContext>(SendCmUploadErrors.MissingUploadServer());
        }
    }

    private static Result<string> ExtractFileCode(string response)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(response);

            string trimmed = response.Trim();

            int jsonStart = trimmed.IndexOf('[');
            if (jsonStart < 0)
                return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse("Expected a JSON array payload."));

            string json = trimmed[jsonStart..];

            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse("Expected an array."));

            if (doc.RootElement.GetArrayLength() == 0)
                return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse("Empty array returned."));

            JsonElement first = doc.RootElement[0];

            if (!first.TryGetProperty("file_code", out JsonElement fileCodeProp))
                return Result.Failure<string>(SendCmUploadErrors.EmptyFileCode());

            string? fileCode = fileCodeProp.GetString();
            if (!string.IsNullOrWhiteSpace(fileCode)
                && !string.Equals(fileCode, "undef", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(fileCode);
            }

            string? fileStatus = null;
            if (first.TryGetProperty("file_status", out JsonElement fileStatusProp))
                fileStatus = fileStatusProp.GetString();

            if (!string.IsNullOrWhiteSpace(fileStatus))
            {
                if (string.Equals(fileStatus, "this file is banned by administrator", StringComparison.OrdinalIgnoreCase))
                    return Result.Failure<string>(SendCmUploadErrors.FileBannedByAdministrator());

                if (string.Equals(
                        fileStatus,
                        "This file has reached the maximum duplicate limit. Please upload an unique file.",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<string>(SendCmUploadErrors.DuplicateLimitReached());
                }

                return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse(fileStatus));
            }

            return Result.Failure<string>(SendCmUploadErrors.EmptyFileCode());
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
