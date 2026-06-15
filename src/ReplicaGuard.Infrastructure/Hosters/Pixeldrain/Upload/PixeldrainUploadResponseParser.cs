using System.Net;
using System.Text.Json;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.Upload;

public class PixeldrainUploadResponseParser
{
    private const string FileTooLargeError = "file_too_large";
    private const string NameTooLongError = "name_too_long";
    private const string WritingError = "writing";
    private const string InternalError = "internal";

    public static async Task<Result<string>> ParseAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            return await ParseErrorAsync(response, ct);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("id", out var idElement))
        {
            return Result.Failure<string>(
                PixeldrainUploadErrors.UnknownError(
                    (int)response.StatusCode,
                    "Missing 'id' in success response"));
        }

        var fileId = idElement.GetString();

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return Result.Failure<string>(
                PixeldrainUploadErrors.UnknownError(
                    (int)response.StatusCode,
                    "Empty 'id' in success response"));
        }

        return Result.Success(fileId);
    }

    private static async Task<Result<string>> ParseErrorAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return Result.Failure<string>(
                PixeldrainUploadErrors.NoFile());
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
                        FileTooLargeError =>
                            Result.Failure<string>(PixeldrainUploadErrors.FileTooLarge()),

                        NameTooLongError =>
                            Result.Failure<string>(PixeldrainUploadErrors.NameTooLong()),

                        WritingError =>
                            Result.Failure<string>(PixeldrainUploadErrors.WritingError()),

                        InternalError =>
                            Result.Failure<string>(PixeldrainUploadErrors.InternalServerError()),

                        _ =>
                            Result.Failure<string>(
                                PixeldrainUploadErrors.UnknownError(
                                    (int)response.StatusCode,
                                    errorValue ?? string.Empty))
                    };
                }
            }
            catch (JsonException ex)
            {
                return Result.Failure<string>(
                    PixeldrainUploadErrors.UnknownError(
                        (int)response.StatusCode,
                        ex.Message));
            }
        }

        var errorContent = await response.Content.ReadAsStringAsync(ct);

        return Result.Failure<string>(
            PixeldrainUploadErrors.UnknownError(
                (int)response.StatusCode,
                errorContent));
    }
}
