using System.Net;
using ReplicaGuard.Application.Abstractions.Caching;

namespace ReplicaGuard.Infrastructure.Captcha;

internal class ScraperHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ICaptchaSolver _captchaSolver;
    private readonly ICacheService _cacheService;

    public ScraperHttpClient(
        HttpClient httpClient,
        ICaptchaSolver captchaSolver,
        ICacheService cacheService)
    {
        _httpClient = httpClient;
        _captchaSolver = captchaSolver;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Core method. Takes a factory function to generate requests, 
    /// allowing for safe retries of POST/PUT payloads and custom headers.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        Func<Task>? onBeforeSolve = null,
        CancellationToken ct = default)
    {
        using var initialRequest = requestFactory();

        if (initialRequest.RequestUri == null)
            throw new ArgumentException("RequestUri cannot be null.", nameof(requestFactory));

        var host = initialRequest.RequestUri.Host;
        var targetRootUrl = initialRequest.RequestUri.GetLeftPart(UriPartial.Authority);
        var cacheKey = $"captcha_session_{host}";

        // 1: Check Cache & Apply
        var session = await _cacheService.GetAsync<CaptchaSession>(cacheKey, ct);
        ApplyHeaders(initialRequest, session);

        // 2: Execute Initial Request
        var response = await _httpClient.SendAsync(initialRequest, ct);

        // 3: Check for Blocks
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.ServiceUnavailable)
        {
            // Dispose the blocked response to free up the socket connection
            response.Dispose();

            await _cacheService.RemoveAsync(cacheKey, ct);

            // 4: Solve Captcha
            session = await _captchaSolver.SolveAsync(targetRootUrl, onBeforeSolve: onBeforeSolve, ct);

            // 5: Update Cache
            await _cacheService.SetAsync(cacheKey, session, TimeSpan.FromMinutes(25), ct);

            // 6: Generate a FRESH request for the retry
            using var retryRequest = requestFactory();
            ApplyHeaders(retryRequest, session);

            // 7: Retry and return
            return await _httpClient.SendAsync(retryRequest, ct);
        }

        return response;
    }

    private static void ApplyHeaders(HttpRequestMessage request, CaptchaSession? session)
    {
        if (session == null) return;

        var cookieHeader = session.BuildCookieHeader();
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            // Remove first to prevent header duplication on custom requests
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        request.Headers.Remove("User-Agent");
        request.Headers.TryAddWithoutValidation("User-Agent", session.UserAgent);
    }
}
