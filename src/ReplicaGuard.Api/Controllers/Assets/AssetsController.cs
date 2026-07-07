using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Application.Assets.GetAsset;
using ReplicaGuard.Application.Assets.ListAssets;
using ReplicaGuard.Application.Replication.ProgressStreaming;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Api.Controllers.Assets;

[ApiController]
[Route("api/assets")]
[Authorize]
public class AssetsController(ISender sender, IReplicaEventStream stream, IUserContext userContext, IAssetRepository assets) : ControllerBase
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
        var hosters = request.Hosters
        .Select(h => new HosterAccountDto(
            HosterId: h.HosterId,
            HosterAccountId: h.AccountId))
        .ToList();

        var command = new CreateAssetCommand(
            request.Source,
            request.FileName,
            hosters);

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

    [HttpGet("{assetId:guid}/events")]
    [Produces("text/event-stream")]
    public Task AssetEvents(
    Guid assetId,
    CancellationToken ct)
    {
        return StreamAssetEvents(
            assetId,
            replicaId: null,
            ct);
    }

    [HttpGet("{assetId:guid}/replicas/{replicaId:guid}/events")]
    [Produces("text/event-stream")]
    public Task ReplicaEvents(
    Guid assetId,
    Guid replicaId,
    CancellationToken ct)
    {
        return StreamAssetEvents(
            assetId,
            replicaId,
            ct);
    }

    private async Task StreamAssetEvents(
        Guid assetId,
        Guid? replicaId,
        CancellationToken ct)
    {
        var userId = userContext.UserId;

        //
        // 0. Load asset
        //
        var asset = await assets.GetByIdAsync(assetId);

        if (asset == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsync("Asset not found", ct);
            return;
        }

        //
        // 1. Determine terminal state
        //
        bool assetTerminal =
            asset.Status is AssetStatus.Completed or AssetStatus.Failed;

        bool replicaTerminal = false;

        if (replicaId.HasValue)
        {
            var replica = asset.Replicas.FirstOrDefault(r => r.Id == replicaId.Value);

            if (replica == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                await Response.WriteAsync("Replica not found", ct);
                return;
            }

            replicaTerminal =
                replica.Status is ReplicaStatus.Completed or ReplicaStatus.Failed;
        }

        //
        // If asset OR replica is terminal → short‑circuit SSE
        //
        if (assetTerminal || replicaTerminal)
        {
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            long.TryParse(Request.Headers["Last-Event-ID"], out var lastEventId);

            //
            // Replay all relevant events
            //
            foreach (var evt in stream.Replay(userId, assetId, lastEventId))
            {
                if (replicaId.HasValue && evt.ReplicaId != replicaId)
                    continue;

                await WriteEvent(evt, ct);
            }

            //
            // Emit final terminal event
            //
            var finalEvt = new ReplicaStreamEvent(
                ReplicaId: replicaId ?? Guid.Empty,
                Status: replicaTerminal ? ReplicaStatus.Completed : ReplicaStatus.Completed,
                BytesTransferred: asset.SizeBytes,
                TotalBytes: asset.SizeBytes,
                OccurredAtUtc: DateTime.UtcNow,
                SequenceNumber: long.MaxValue
            );

            await WriteEvent(finalEvt, ct);

            return;
        }

        //
        // 2. Asset and replica are active → normal SSE streaming
        //
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        long.TryParse(Request.Headers["Last-Event-ID"], out var liveLastEventId);

        var subscription = stream.Subscribe(userId, assetId);

        try
        {
            //
            // Replay missed events
            //
            foreach (var evt in stream.Replay(userId, assetId, liveLastEventId))
            {
                if (replicaId.HasValue && evt.ReplicaId != replicaId)
                    continue;

                await WriteEvent(evt, ct);
                liveLastEventId = evt.SequenceNumber;
            }

            //
            // Live stream
            //
            await foreach (var evt in subscription.Reader.ReadAllAsync(ct))
            {
                if (evt.SequenceNumber <= liveLastEventId)
                    continue;

                if (replicaId.HasValue && evt.ReplicaId != replicaId)
                    continue;

                await WriteEvent(evt, ct);
                liveLastEventId = evt.SequenceNumber;
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected
        }
        finally
        {
            stream.Unsubscribe(userId, assetId, subscription);
        }
    }


    private async Task WriteEvent(
    ReplicaStreamEvent evt,
    CancellationToken ct)
    {
        await Response.WriteAsync(
            $"id: {evt.SequenceNumber}\n",
            ct);

        await Response.WriteAsync(
            "event: replica_progress\n",
            ct);

        await Response.WriteAsync(
            $"data: {JsonSerializer.Serialize(evt)}\n\n",
            ct);

        await Response.Body.FlushAsync(ct);
    }
}
