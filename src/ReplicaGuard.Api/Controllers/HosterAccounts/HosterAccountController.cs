using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;
using ReplicaGuard.Application.HosterAccounts.GetHosterAccount;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Api.Controllers.HosterAccounts;

[ApiController]
[Route("api/hoster-accounts")]
public sealed class HosterAccountController : ControllerBase
{
    private readonly ISender _sender;

    public HosterAccountController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new HosterAccount for the authenticated user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateHosterAccountRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Parse HosterCode safely
        if (!Enum.TryParse<HosterCode>(request.HosterId, true, out var hosterCode))
        {
            return BadRequest(new
            {
                error = $"Invalid hoster code '{request.HosterId}'."
            });
        }

        // 2. Map API identity payloads => Application IdentityDto
        var identities = request.Identities
            .Select(IdentityMapper.MapIdentity)
            .ToList();

        // 3. Build application command
        var command = new CreateHosterAccountCommand(
            hosterCode,
            request.Alias,
            request.Description,
            identities);

        // 4. Execute command
        var result = await _sender.Send(command, cancellationToken);

        // 5. Return response
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.HosterAccountId }, result.Value)
            : result.ToActionResult();
    }

    /// <summary>
    /// Gets a single HosterAccount by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetHosterAccountQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToActionResult();
    }
}
