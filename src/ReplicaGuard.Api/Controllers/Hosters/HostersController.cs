using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.Hosters;
using ReplicaGuard.Application.Hosters.GetHoster;
using ReplicaGuard.Application.Hosters.ListHosters;

namespace ReplicaGuard.Api.Controllers.Hosters;

[ApiController]
[Route("api/hosters")]
public class HostersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// List all supported hosters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<HosterResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        ListHostersQuery query = new();

        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToActionResult();
    }

    /// <summary>
    /// Get a specific hoster by friendly name.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(HosterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var query = new GetHosterQuery(id);
        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToActionResult();
    }

}
