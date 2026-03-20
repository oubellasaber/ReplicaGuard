using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Common;

namespace ReplicaGuard.Core.Domain.User;
public class UserErrors
{
    public static Error NotFound(Guid id) => CommonErrors.NotFound(nameof(User), id);
    public static Error NotFoundByEmail(string email) => CommonErrors.NotFound(nameof(User), "email", email);
    public static Error UsernameAlreadyTaken(string username) =>
        new Error(
            code: "User.UsernameAlreadyTaken",
            message: "Username Already Taken"
        )
        .WithDetail($"The username '{username}' is already in use.")
        .WithType(ErrorType.Conflict);

    public static Error EmailAlreadyTaken(string email) =>
        new Error(
            code: "User.EmailAlreadyTaken",
            message: "Email Already Taken"
        )
        .WithDetail($"The email address '{email}' is already registered.")
        .WithType(ErrorType.Conflict);

    public static readonly Error InvalidCredentials =
       new Error(code: "User.InvalidCredentials", message: "Invalid Credentials")
       .WithDetail("The email or password you entered is incorrect.")
       .WithType(ErrorType.Unauthorized);
}
