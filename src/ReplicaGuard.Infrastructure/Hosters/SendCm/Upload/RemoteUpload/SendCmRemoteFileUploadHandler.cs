using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.Upload.RemoteUpload;

internal class SendCmRemoteFileUploadHandler : IRemoteFileUploadHandler
{
    private readonly SendCmOptions _options;
    private readonly HttpClient _httpClient;
    private readonly HttpClient _uploadClient;
    private readonly SendCmUploadSessionProvider _sessionProvider;
    private readonly ISecretEncryptionService _crypto;

    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.RemoteFileUpload;

    public SendCmRemoteFileUploadHandler(
        IOptions<SendCmOptions> options,
        IHttpClientFactory factory,
        SendCmUploadSessionProvider sessionProvider,
        ISecretEncryptionService crypto)
    {
        _options = options.Value;
        _uploadClient = factory.CreateClient("FileUploadingHttpClient");
        _httpClient = factory.CreateClient();
        _crypto = crypto;
        _sessionProvider = sessionProvider;
    }

    public async Task<Result<RemoteFileUploadResponse>> HandleAsync(RemoteFileUploadRequest input, CancellationToken ct = default)
    {
        var apiKeyIdentity = input.Account
            .Identities
            .FirstOrDefault(id => id.Type == IdentityType.ApiKey);

        if (apiKeyIdentity is null || apiKeyIdentity.Status != IdentityVerificationStatus.Verified)
        {
            throw new InvalidOperationException("The account does not have a verified API key identity.");
        }
        
        var decryptedApiKey = apiKeyIdentity.RevealSecret(SecretType.ApiKeyPair, _crypto);
        var sessionResult = await _sessionProvider.GetSessionAsync(decryptedApiKey, ct);
        if (sessionResult.IsFailure)
            return Result.Failure<RemoteFileUploadResponse>(sessionResult.Error);
        var session = sessionResult.Value;

        string uploadId = GenerateRandomUploadId();
        string uploadUrl = $"{session.UploadServer}?upload_type=url&upload_id={uploadId}";

        using MultipartFormDataContent content = new()
        {
            { new StringContent(session.SessionId), "sess_id" },
            { new StringContent("reg"), "utype" },
            { new StringContent("1"), "file_public" },
            { new StringContent(input.FileName, Encoding.UTF8), "name" },
            { new StringContent(input.Source.Url.Value.ToString()), "url_mass" },
            { new StringContent("", Encoding.UTF8), "link_pass" },
            { new StringContent(""), "to_folder" },
            { new StringContent("1"), "add_my_acc" },
            { new StringContent("1"), "keepalive" }
        };

        byte[] reqBody = await content.ReadAsByteArrayAsync(ct);

        using ByteArrayContent buffered = new(reqBody);

        foreach (var header in content.Headers)
        {
            buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using HttpRequestMessage request = new(HttpMethod.Post, uploadUrl)
        {
            Content = buffered,
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        request.Headers.ExpectContinue = false;

        using HttpResponseMessage response =
            await _uploadClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<RemoteFileUploadResponse>(
                SendCmUploadErrors.HttpFailure(uploadUrl, response.StatusCode));
        }

        string body = await response.Content.ReadAsStringAsync(ct);

        var fileCodeResult = await SendCmFileCodeParser.ParseAsync(response, ct);
        if (fileCodeResult.IsFailure)
        {
            return Result.Failure<RemoteFileUploadResponse>(fileCodeResult.Error);
        }

        var fileCode = fileCodeResult.Value;
        var fileUrl = new Uri($"{_options.ApiBaseUrl}/{fileCode}");

        string updateStatUrl = $"{new Uri(session.UploadServer).GetLeftPart(UriPartial.Authority)}/tmp/{uploadId}.json";
        using HttpResponseMessage updateStatResponse = await _httpClient.GetAsync(updateStatUrl, ct);
        string updateStatBody = await updateStatResponse.Content.ReadAsStringAsync(ct);
        var updateStat = SendCmUpdateStatParser.Parse(updateStatBody);
        if (updateStat.IsFailure)
        {
            return Result.Failure<RemoteFileUploadResponse>(updateStat.Error);
        }

        long sizeBytes = updateStat.Value.Total;

        return Result.Success(new RemoteFileUploadResponse(fileCode, fileUrl, input.FileName, sizeBytes));
    }

    private static string GenerateRandomUploadId()
        => string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(10)));
}
