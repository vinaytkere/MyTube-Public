using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Downloads.Commands;
using MyTube.Backend.Application.Features.Downloads.Queries;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
[Produces("application/json")]
public class DownloadController : ControllerBase
{
    private readonly ISender _sender;

    public DownloadController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(DownloadJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DownloadJobDto>> StartDownload([FromBody] DownloadRequest request, CancellationToken ct)
    {
        var job = await _sender.Send(new StartDownloadCommand(request), ct);
        return Ok(job);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchDownloadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchDownloadResponseDto>> StartBatchDownload([FromBody] BatchDownloadRequest request, CancellationToken ct)
    {
        var response = await _sender.Send(new StartBatchDownloadCommand(request), ct);
        return Ok(response);
    }

    [HttpGet("batches")]
    [ProducesResponseType(typeof(List<DownloadBatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DownloadBatchDto>>> GetBatches(CancellationToken ct)
    {
        var batches = await _sender.Send(new GetDownloadBatchesQuery(), ct);
        return Ok(batches);
    }

    [HttpGet("batches/{id}")]
    [ProducesResponseType(typeof(DownloadBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DownloadBatchDto>> GetBatch(Guid id, CancellationToken ct)
    {
        var batch = await _sender.Send(new GetBatchDetailsQuery(id), ct);
        if (batch == null)
        {
            return NotFound(new { message = "Batch not found." });
        }
        return Ok(batch);
    }

    [HttpGet("probe-playlist")]
    [ProducesResponseType(typeof(PlaylistProbeResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlaylistProbeResultDto>> ProbePlaylist([FromQuery] string url, CancellationToken ct)
    {
        var result = await _sender.Send(new ProbePlaylistQuery(url), ct);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<DownloadJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DownloadJobDto>>> GetJobs(CancellationToken ct)
    {
        var jobs = await _sender.Send(new GetDownloadJobsQuery(), ct);
        return Ok(jobs);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelJob(Guid id, CancellationToken ct)
    {
        var cancelled = await _sender.Send(new CancelDownloadJobCommand(id), ct);
        if (!cancelled)
        {
            return NotFound(new { message = "Job not found or cannot be cancelled." });
        }
        return Ok(new { message = "Job cancellation request sent." });
    }

    [HttpPost("batches/{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBatch(Guid id, CancellationToken ct)
    {
        var cancelled = await _sender.Send(new CancelBatchCommand(id), ct);
        if (!cancelled)
        {
            return NotFound(new { message = "Batch not found or already completed/cancelled." });
        }
        return Ok(new { message = "Batch cancellation requested." });
    }

    [HttpDelete("batches/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBatch(Guid id, CancellationToken ct)
    {
        var deleted = await _sender.Send(new DeleteBatchCommand(id), ct);
        if (!deleted)
        {
            return NotFound(new { message = "Batch not found." });
        }
        return Ok(new { message = "Batch deleted." });
    }

    [HttpPost("cancel-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAll(CancellationToken ct)
    {
        await _sender.Send(new CancelAllDownloadsCommand(), ct);
        return Ok(new { message = "All active downloads and batches cancellation requested." });
    }

    [HttpPost("clear-finished")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearFinished(CancellationToken ct)
    {
        await _sender.Send(new ClearFinishedDownloadsCommand(), ct);
        return Ok(new { message = "Completed, failed, and cancelled downloads cleared." });
    }

    [HttpGet("{id}/download")]
    [HttpGet("{id}/file")]
    [HttpHead("{id}/download")]
    [HttpHead("{id}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadJobFile(Guid id, CancellationToken ct)
    {
        var fileResult = await _sender.Send(new GetDownloadJobFileQuery(id), ct);
        if (fileResult == null)
        {
            return NotFound(new { message = "Job not found or downloaded file is not available." });
        }

        return PhysicalFile(fileResult.FilePath, fileResult.ContentType, fileDownloadName: fileResult.FileName, enableRangeProcessing: true);
    }

    [HttpGet("probe")]
    [ProducesResponseType(typeof(VideoProbeResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VideoProbeResultDto>> ProbeVideo([FromQuery] string url, CancellationToken ct)
    {
        var result = await _sender.Send(new ProbeVideoQuery(url), ct);
        return Ok(result);
    }

    [HttpGet("qualities")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetQualities([FromQuery] string url, CancellationToken ct)
    {
        var qualities = await _sender.Send(new GetAvailableQualitiesQuery(url), ct);
        return Ok(qualities);
    }

    [HttpPost("update-binary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult UpdateBinary()
    {
        _ = _sender.Send(new UpdateYtDlpBinaryCommand(), CancellationToken.None);
        return Ok(new { message = "yt-dlp update process triggered in background." });
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(DownloaderSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DownloaderSettingsDto>> GetSettings(CancellationToken ct)
    {
        var settings = await _sender.Send(new GetDownloaderSettingsQuery(), ct);
        return Ok(settings);
    }

    [HttpPost("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] DownloaderSettingsDto settings, CancellationToken ct)
    {
        await _sender.Send(new UpdateDownloaderSettingsCommand(settings), ct);
        return Ok(new { success = true, message = "Downloader settings saved successfully." });
    }
}
