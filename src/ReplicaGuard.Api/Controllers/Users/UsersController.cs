using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.Users;
using ReplicaGuard.Application.Users.LogInUser;
using ReplicaGuard.Application.Users.RefreshToken;
using ReplicaGuard.Application.Users.RegisterUser;

namespace ReplicaGuard.Api.Controllers.Users;

[ApiController]
[Route("api/users")]
[AllowAnonymous]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("register")]
    [EndpointSummary("Register a new user")]
    [EndpointDescription("Creates a new user account with the provided registration details and returns access tokens for immediate authentication.")]
    [ProducesResponseType<AccessTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Name,
            request.Email,
            request.Password,
            request.ConfirmationPassword);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value);
    }

    [HttpPost("login")]
    [EndpointSummary("Authenticate user")]
    [EndpointDescription("Authenticates a user with their email and password, returning access and refresh tokens upon successful login.")]
    [ProducesResponseType<AccessTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogIn(
        LogInUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LogInUserCommand(request.Email, request.Password);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    [EndpointSummary("Refresh authentication tokens")]
    [EndpointDescription("Issues new access and refresh tokens using a valid refresh token, extending the user's authenticated session.")]
    [ProducesResponseType<AccessTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value);
    }
}
