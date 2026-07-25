namespace ReplicaGuard.Infrastructure.Captcha;

public interface ICaptchaSolver
{
    Task<CaptchaSession> SolveAsync(
        string targetUrl,
        Func<Task>? onBeforeSolve = null,
        CancellationToken ct = default);
}

public record CaptchaSession(
    Dictionary<string, string> Cookies,
    string UserAgent)
{
    public string BuildCookieHeader()
    {
        if (Cookies == null || Cookies.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", Cookies.Select(c => $"{c.Key}={c.Value}"));
    }
}
