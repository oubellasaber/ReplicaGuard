using System.Net;
using Microsoft.AspNetCore.Identity;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Application.Users;

public static class AuthenticationErrors
{
    public static Error FromIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var error = new Error("Identity.ValidationFailed", "Identity validation failed")
            .WithDetail($"One or more fields are invalid.")
            .WithMetadata("errors", errors.Select(e => new
            {
                e.Code,
                e.Description
            }).ToList());

        return error;
    }

    public static Error InvalidCredentials =>
        new Error("Authentication.InvalidCredentials", "Invalid credentials provided");

    public static Error InvalidField(string field, string? detail = null) =>
        new Error("Authentication.FieldInvalid", $"{field} is invalid")
            .WithDetail(detail ?? $"Invalid value provided for {field}.")
            .WithMetadata("field", field);

    public static Error InvalidRefreshToken =
        new Error(
            code: "Authentication.InvalidRefreshToken",
            message: "Invalid Refresh Token"
        )
        .WithDetail("The provided refresh token is invalid or has expired.");
}
