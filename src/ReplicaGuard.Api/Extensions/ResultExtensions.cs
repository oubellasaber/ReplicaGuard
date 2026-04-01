using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Api.Extensions;

public static class ResultExtensions
{
    public static int GetStatusCode(this Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };
    }

    public static ProblemDetails ToProblemDetails(this Error error)
    {
        int statusCode = error.GetStatusCode();

        var problemDetails = new ProblemDetails
        {
            Type = GetRfcUri(statusCode),
            Title = error.Message,
            Detail = error.Detail,
            Status = statusCode,
            Extensions = { ["code"] = error.Code }
        };

        foreach (var kvp in error.Metadata)
            problemDetails.Extensions[kvp.Key] = kvp.Value;

        return problemDetails;
    }

    public static ProblemDetails ToProblemDetails(this Result result)
    {
        return result.IsSuccess
            ? throw new InvalidOperationException("Cannot create ProblemDetails from a successful result.")
            : result.Error.ToProblemDetails();
    }

    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        var problem = result.ToProblemDetails();
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult> onSuccess)
    {
        return result.IsSuccess
            ? onSuccess(result.Value)
            : result.ToActionResult();
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, Func<IActionResult> onSuccess)
    {
        return result.IsSuccess
            ? onSuccess()
            : result.ToActionResult();
    }

    public static string GetRfcUri(int statusCode) => statusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };
}
