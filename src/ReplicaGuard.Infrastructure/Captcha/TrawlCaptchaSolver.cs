using System.Net.Http.Json;

namespace ReplicaGuard.Infrastructure.Captcha;

internal record TrawlCookie(
    string Name,
    string Value,
    string Domain,
    string Path,
    double Expires,
    bool HttpOnly,
    bool Secure,
    string SameSite
);

internal record TrawlScrapeResponse(
    string Url,
    string Html,
    int StatusCode,
    List<TrawlCookie> Cookies,
    int TotalMs,
    bool ProxyUsed,
    string UserAgent
);

public class TrawlCaptchaSolver : ICaptchaSolver
{
    private readonly HttpClient _trawlClient;

    public TrawlCaptchaSolver(HttpClient trawlClient)
    {
        _trawlClient = trawlClient;
    }

    public async Task<CaptchaSession> SolveAsync(
        string targetUrl,
        Func<Task>? onBeforeSolve = null,
        CancellationToken ct = default)
    {
        if (onBeforeSolve != null)
        {
            await onBeforeSolve();
        }

        // Hit the actual /scrape endpoint of Trawl instance
        var payload = new { url = targetUrl, maxTimeout = 60000 };
        using var response = await _trawlClient.PostAsJsonAsync("/scrape", payload, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TrawlScrapeResponse>(cancellationToken: ct);

        if (result?.Cookies == null || result.Cookies.Count == 0)
        {
            throw new InvalidOperationException("Captcha solver failed: The returned cookies array is empty.");
        }

        var targetHost = new Uri(targetUrl).Host;

        // Domain filtering and freshness deduplication
        var filteredCookies = result.Cookies
            .Where(c => !string.IsNullOrEmpty(c.Domain) &&
                        targetHost.EndsWith(c.Domain.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(c => c.Expires) // Pick newest if duplicates exist
                .First())
            .ToDictionary(c => c.Name, c => c.Value, StringComparer.OrdinalIgnoreCase);

        if (filteredCookies.Count == 0)
        {
            throw new InvalidOperationException($"Captcha solver failed: No cookies matched target domain '{targetHost}'.");
        }

        return new CaptchaSession(filteredCookies, result.UserAgent);
    }
}
