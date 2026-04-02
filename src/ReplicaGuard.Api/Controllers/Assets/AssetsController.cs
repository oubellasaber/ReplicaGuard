using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Application.Assets.GetAsset;
using ReplicaGuard.Application.Assets.ListAssets;

namespace ReplicaGuard.Api.Controllers.Assets;

[ApiController]
[Route("api/assets")]
[Authorize]
public class AssetsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Create a new asset and begin replication to the specified hosters.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateAssetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssetRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAssetCommand(
            request.Source,
            request.FileName,
            request.HosterIds);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.AssetId }, result.Value)
            : result.ToActionResult();
    }

    /// <summary>
    /// Get a specific asset with all its replicas.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetAssetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAssetQuery(id);

        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToActionResult();
    }

    /// <summary>
    /// List all assets for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<AssetSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var query = new ListAssetsQuery();

        var result = await sender.Send(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToActionResult();
    }
}
