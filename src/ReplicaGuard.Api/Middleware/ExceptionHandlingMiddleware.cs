using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Infrastructure.Storage;
using ValidationException = ReplicaGuard.Application.Exceptions.ValidationException;

namespace ReplicaGuard.Api.Middleware;

internal sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

            ExceptionDetails exceptionDetails = GetExceptionDetails(exception);

            var problemDetails = new ProblemDetails
            {
                Status = exceptionDetails.Status,
                Type = exceptionDetails.Type,
                Title = exceptionDetails.Title,
                Detail = exceptionDetails.Detail,
            };

            if (exceptionDetails.Errors is not null)
            {
                problemDetails.Extensions["errors"] = exceptionDetails.Errors;
            }

            context.Response.StatusCode = exceptionDetails.Status;

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }

    private static ExceptionDetails GetExceptionDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => new ExceptionDetails(
                StatusCodes.Status400BadRequest,
                ResultExtensions.GetRfcUri(400),
                "Validation error",
                "One or more validation errors has occurred",
                validationException.Errors),
            FileTooLargeException fileTooLargeException => new ExceptionDetails(
                StatusCodes.Status413PayloadTooLarge,
                ResultExtensions.GetRfcUri(413),
                "File too large",
                $"The uploaded file exceeds the maximum allowed size of {fileTooLargeException.LimitBytes:N0} bytes.",
                null),
            _ => new ExceptionDetails(
                StatusCodes.Status500InternalServerError,
                ResultExtensions.GetRfcUri(500),
                "Server error",
                "An unexpected error has occurred",
                null)
        };
    }

    internal sealed record ExceptionDetails(
        int Status,
        string Type,
        string Title,
        string Detail,
        IEnumerable<object>? Errors);
}
