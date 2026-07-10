using Microsoft.Net.Http.Headers;

namespace ReplicaGuard.Api.Controllers.Assets;

public static class MultipartRequestHelper
{
    public static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

        if (string.IsNullOrWhiteSpace(boundary))
            throw new InvalidOperationException("Missing content-type boundary.");

        if (boundary.Length > lengthLimit)
            throw new InvalidOperationException($"Multipart boundary length limit {lengthLimit} exceeded.");

        return boundary;
    }

    public static bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
    {
        return contentDisposition != null
            && contentDisposition.DispositionType.Equals("form-data")
            && string.IsNullOrEmpty(contentDisposition.FileName.Value)
            && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
    }

    public static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
    {
        return contentDisposition != null
            && contentDisposition.DispositionType.Equals("form-data")
            && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
    }
}
