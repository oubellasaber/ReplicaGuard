using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;
using ReplicaGuard.Application.HosterCredentials.GetHosterCredentials;
using ReplicaGuard.Application.HosterCredentials.UpdateHosterCredentials;

namespace ReplicaGuard.Api.Controllers.HosterCredentials;

[ApiController]
[Route("api/hosters/{hosterId:required}/credentials")]
[Authorize]
public class CredentialsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Add credentials for a specific hoster.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AddHosterCredentialsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddCredentials(
        [FromRoute] Guid hosterId,
        [FromBody] AddCredentialsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddHosterCredentialsCommand(
            hosterId,
            request.ApiKey,
            request.Username,
            request.Email,
            request.Password);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetCredentials), new { hosterId }, result.Value)
            : result.ToActionResult();
    }

    /// <summary>
    /// Get credentials for a specific hoster.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GetCredentialsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredentials(
        [FromRoute] Guid hosterId,
        CancellationToken cancellationToken)
    {
        var query = new GetHosterCredentialsQuery(hosterId);

        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return result.ToActionResult();

        var response = new GetCredentialsResponse(
            result.Value.Id,
            result.Value.HosterId,
            result.Value.ApiKey,
            result.Value.Username,
            result.Value.Email,
            result.Value.Password,
            result.Value.Status,
            result.Value.CreatedAt,
            result.Value.UpdatedAt);

        return Ok(response);
    }

    /// <summary>
    /// Update credentials for a specific hoster.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCredentials(
        [FromRoute] Guid hosterId,
        [FromBody] UpdateCredentialsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateHosterCredentialsCommand(
            hosterId,
            request.ApiKey,
            request.Username,
            request.Email,
            request.Password);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : result.ToActionResult();
    }
}
