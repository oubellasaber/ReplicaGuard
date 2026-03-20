using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace ReplicaGuard.Api.Middleware;

internal sealed class RequestContextLoggingMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        string correlationId = GetCorrelationId(context);

        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        context.Request.Headers.TryGetValue(
            CorrelationIdHeaderName,
            out StringValues correlationId);

        string? value = correlationId.FirstOrDefault();

        return string.IsNullOrWhiteSpace(value)
            ? context.TraceIdentifier
            : value;
    }
}
