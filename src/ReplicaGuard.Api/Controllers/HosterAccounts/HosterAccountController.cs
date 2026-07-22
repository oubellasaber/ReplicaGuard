using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;
using ReplicaGuard.Application.HosterAccounts.GetHosterAccount;
using ReplicaGuard.Application.HosterAccounts.GetHosterAccounts;

namespace ReplicaGuard.Api.Controllers.HosterAccounts;

[ApiController]
[Route("api/hoster-accounts")]
[Authorize]
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
        // 1. Map API identity payloads => Application IdentityDto
        var identities = request.Identities
            .Select(IdentityMapper.MapIdentity)
            .ToList();

        // 2. Build application command
        var command = new CreateHosterAccountCommand(
            request.HosterId,
            request.Alias,
            request.Description,
            identities);

        // 3. Execute command
        var result = await _sender.Send(command, cancellationToken);

        // 4. Return response
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

    /// <summary>
    /// Gets All HosterAccounts
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
    [FromQuery] PagedResourceParameters parameters,
    CancellationToken cancellationToken)
    {
        var query = new GetHosterAccountsQuery(parameters);
        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToActionResult();
    }
}
