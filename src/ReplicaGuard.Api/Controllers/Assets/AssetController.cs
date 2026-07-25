using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ReplicaGuard.Api.Extensions;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Application.Assets.CreateAsset.CreateLocalAsset;
using ReplicaGuard.Application.Assets.CreateAsset.CreateRemoteAsset;
using ReplicaGuard.Application.Assets.GetAsset;
using ReplicaGuard.Application.Assets.ListAssets;
using ReplicaGuard.Application.Replication.ProgressStreaming;
using ReplicaGuard.Application.Replicas.GenerateDownloadUrl;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Cleanup;
using ReplicaGuard.Infrastructure.Storage;

namespace ReplicaGuard.Api.Controllers.Assets;

[ApiController]
[Route("api/assets")]
[Authorize]
public class AssetController(
    ISender sender,
    IReplicaEventStream stream,
    IUserContext userContext,
    IAssetRepository assets,
    IOptions<UserUploadsOptions> userUploadsOptions,
    IOptions<StorageOptions> storageOptions) : ControllerBase
{
    private readonly UserUploadsOptions _userUploadsOptions = userUploadsOptions.Value;
    private readonly StorageOptions _storageOptions = storageOptions.Value;

    /// <summary>
    /// Create a new asset and begin replication to the specified hosters.
    /// </summary>
    [HttpPost("uploads/remote")]
    [ProducesResponseType(typeof(CreateAssetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssetRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRemoteAssetCommand(
            request.Source,
            request.FileName,
            request.HosterAccountIds);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.AssetId }, result.Value)
            : result.ToActionResult();
    }

    [HttpPost("uploads/local")]
    [DisableFormValueModelBinding]
    [DisableRequestSizeLimit]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var assetId = Guid.NewGuid();
        string? finalPath = null;

        try
        {
            (var tempPath, var fileName, var hostersRaw) = await ParseMultipartAsync(Request, assetId, ct);

            finalPath = tempPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                ? tempPath[..^4]
                : tempPath;

            if (finalPath != tempPath)
            {
                System.IO.File.Move(tempPath, finalPath);
            }

            List<Guid> hosterAccsIds;
            try
            {
                hosterAccsIds = hostersRaw
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => Guid.Parse(x))
                    .ToList();
            }
            catch (FormatException)
            {
                System.IO.File.Delete(finalPath);
                return BadRequest(new { error = "Invalid hoster account ID format." });
            }

            var command = new CreateLocalAssetCommand(assetId, _userUploadsOptions.UploadDirectory, finalPath, fileName, hosterAccsIds);
            var result = await sender.Send(command, ct);

            if (result.IsFailure)
            {
                System.IO.File.Delete(finalPath);
                return result.ToActionResult();
            }

            return CreatedAtAction(nameof(Get), new { id = result.Value.AssetId }, result.Value);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch
        {
            if (finalPath is not null)
                System.IO.File.Delete(finalPath);
            throw;
        }
    }

    private async Task<(string TempPath, string FileName, string HostersRaw)> ParseMultipartAsync(
       HttpRequest request, Guid assetId, CancellationToken ct)
    {
        string? fileName = null;
        string? hostersRaw = null;
        string? tempPath = null;

        var boundary = MultipartRequestHelper.GetBoundary(
            MediaTypeHeaderValue.Parse(request.ContentType),
            int.MaxValue);

        var reader = new MultipartReader(boundary, request.Body);

        MultipartSection? section;

        while ((section = await reader.ReadNextSectionAsync(ct)) != null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd))
                continue;

            if (MultipartRequestHelper.HasFileContentDisposition(cd))
            {
                if (fileName is null)
                    throw new InvalidOperationException("fileName must be provided before file.");

                var uploadsDir = _userUploadsOptions.UploadDirectory;
                Directory.CreateDirectory(uploadsDir);

                tempPath = Path.Combine(uploadsDir, $"upl_{assetId}_{SanitizeFileName(fileName)}.tmp");

                try
                {
                    using var fs = System.IO.File.Create(tempPath);
                    using var countingStream = new CountingStream(fs, _storageOptions.MaxFileSizeBytes);
                    await section.Body.CopyToAsync(countingStream, ct);
                }
                catch
                {
                    if (tempPath is not null)
                        System.IO.File.Delete(tempPath);
                    throw;
                }

                continue;
            }

            if (MultipartRequestHelper.HasFormDataContentDisposition(cd))
            {
                using var sr = new StreamReader(section.Body);
                var value = await sr.ReadToEndAsync();

                switch (cd.Name.Value)
                {
                    case "fileName":
                        fileName = value;
                        break;

                    case "hosters":
                        hostersRaw = value;
                        break;
                }
            }
        }

        if (tempPath is null)
            throw new InvalidOperationException("File missing.");

        if (fileName is null)
            throw new InvalidOperationException("fileName missing.");

        if (hostersRaw is null)
            throw new InvalidOperationException("hosters missing.");

        return (tempPath, fileName, hostersRaw);

        static string SanitizeFileName(string fileName)
        {           
            var name = Path.GetFileName(fileName);
            var invalid = Path.GetInvalidFileNameChars();  
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }
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
    /// List all assets for the current user with support for filtering, sorting, and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedList<AssetSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] ResourceParameters parameters,
        CancellationToken cancellationToken)
    {
        var query = new ListAssetsQuery(parameters);

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

    /// <summary>
    /// Generate a direct download URL for the specified replica.
    /// </summary>
    [HttpPost("{assetId:guid}/replicas/{replicaId:guid}/download-url")]
    [ProducesResponseType(typeof(GenerateDownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateDownloadUrl(
        [FromRoute] Guid assetId,
        [FromRoute] Guid replicaId,
        CancellationToken cancellationToken)
    {
        var command = new GenerateDownloadUrlCommand(assetId, replicaId);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.ToActionResult();
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
        // If asset OR replica is terminal => short‑circuit SSE
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
