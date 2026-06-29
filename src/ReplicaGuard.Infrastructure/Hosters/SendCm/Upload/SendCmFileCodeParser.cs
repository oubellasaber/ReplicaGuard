using System.Text.Json;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.Upload;

internal static class SendCmFileCodeParser
{
    public static async Task<Result<string>> ParseAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var body = await response.Content.ReadAsStringAsync();
            int jsonStart = body.IndexOf('[');
            if (jsonStart < 0)
                return Result.Failure<string>(SendCmUploadErrors.InvalidJsonResponse("Expected a JSON array payload."));

            string json = body[jsonStart..];

            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Result.Failure<string>(
                    SendCmUploadErrors.InvalidJsonResponse("Expected array"));

            if (doc.RootElement.GetArrayLength() == 0)
                return Result.Failure<string>(
                    SendCmUploadErrors.InvalidJsonResponse("Empty array"));

            JsonElement first = doc.RootElement[0];

            if (!first.TryGetProperty("file_code", out var fileCodeProp))
                return Result.Failure<string>(SendCmUploadErrors.EmptyFileCode());

            var fileCode = fileCodeProp.GetString();

            if (!string.IsNullOrWhiteSpace(fileCode) &&
                !string.Equals(fileCode, "undef", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(fileCode);
            }

            return Result.Failure<string>(SendCmUploadErrors.EmptyFileCode());
        }
        catch (JsonException ex)
        {
            return Result.Failure<string>(
                SendCmUploadErrors.InvalidJsonResponse(ex.Message));
        }
    }
}
