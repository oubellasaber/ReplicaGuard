using System.Net.Http.Headers;
using System.Text;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain;

internal static class PixeldrainBasicAuthenticationHeaderFactory
{
    public static AuthenticationHeaderValue Create(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        var credentials = ":" + apiKey;
        var bytes = Encoding.UTF8.GetBytes(credentials);
        var base64 = Convert.ToBase64String(bytes);

        return new AuthenticationHeaderValue("Basic", base64);
    }
}
