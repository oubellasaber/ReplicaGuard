using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.Upload.LocalUpload;

internal sealed class SendCmLocalFileUploadHandler : ILocalFileUploadHandler
{
    private readonly SendCmOptions _options;
    private readonly HttpClient _uploadClient;
    private readonly SendCmUploadSessionProvider _sessionProvider;
    private readonly ISecretEncryptionService _crypto;

    public SendCmLocalFileUploadHandler(
        IOptions<SendCmOptions> options,
        IHttpClientFactory factory,
        SendCmUploadSessionProvider sessionProvider,
        ISecretEncryptionService crypto)
    {
        _options = options.Value;
        _uploadClient = factory.CreateClient("FileUploadingHttpClient");
        _crypto = crypto;
        _sessionProvider = sessionProvider;
    }

    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.LocalFileUpload;

    public async Task<Result<LocalFileUploadResponse>> HandleAsync(LocalFileUploadRequest input, CancellationToken ct = default)
    {
        var apiKeyIdentity = input.Account
            .Identities
            .FirstOrDefault(id => id.Type == IdentityType.ApiKey);

        if (apiKeyIdentity is null)
        {
            return Result.Failure<LocalFileUploadResponse>(
                SendCmUploadErrors.IdentityMissing(input.Account.Id, IdentityType.ApiKey));
        }
        else if (apiKeyIdentity.Status != IdentityVerificationStatus.Verified)
        {
            return Result.Failure<LocalFileUploadResponse>(
                SendCmUploadErrors.IdentityNotVerified(input.Account.Id, apiKeyIdentity.Id));
        }

        var decryptedApiKey = apiKeyIdentity.RevealSecret(SecretType.ApiKeyPair, _crypto);
        var fileName = input.FileName;
        var filePath = input.Source.FilePath;

        if (!File.Exists(filePath))
            return Result.Failure<LocalFileUploadResponse>(
                HosterErrors.LocalFileNotFound(filePath));

        var sessionResult = await _sessionProvider.GetSessionAsync(decryptedApiKey, ct);
        if (sessionResult.IsFailure)
            return Result.Failure<LocalFileUploadResponse>(sessionResult.Error);
        var session = sessionResult.Value;

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        await using var progressStream = new ProgressStream(
            fileStream,
            input.OnProgress,
            leaveOpen: true);

        var uploadUrl = $"{session.UploadServer}?upload_type=file&utype=reg";
        var content = new RawMultipartFormDataContent(
            new Dictionary<string, string>
            {
                ["sess_id"] = session.SessionId,
                ["utype"] = "reg",
                ["to_folder"] = "0",
                ["file_public"] = "",
                ["relativePath"] = "null",
                ["file_expire_unit"] = "",
                ["file_expire_time"] = "",
                ["file_max_dl"] = "",
                ["link_pass"] = "",
                ["link_rcpt"] = "",
            },
            fileStream,
            fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
        {
            Content = content
        };

        using var response = await _uploadClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<LocalFileUploadResponse>(
                SendCmUploadErrors.HttpFailure(uploadUrl, response.StatusCode));
        }

        var fileCodeResult = await SendCmFileCodeParser.ParseAsync(response, ct);
        if (fileCodeResult.IsFailure)
        {
            return Result.Failure<LocalFileUploadResponse>(fileCodeResult.Error);
        }

        var fileCode = fileCodeResult.Value;
        var fileUrl = new Uri($"{_options.ApiBaseUrl}/{fileCode}");

        return Result.Success(new LocalFileUploadResponse(fileCode, fileUrl, fileName, fileStream.Length));
    }
}
