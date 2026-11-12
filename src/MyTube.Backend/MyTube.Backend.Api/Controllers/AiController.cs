using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application.Features.AI.Commands;
using MyTube.Backend.Application.Features.AI.Queries;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Produces("application/json")]
public class AiController : ControllerBase
{
    private readonly ISender _sender;

    public AiController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Trigger AI audio transcription, summary, tags, language detection, and virtual categorization for a single video.
    /// </summary>
    [HttpPost("videos/{id:guid}/analyze")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeVideo(Guid id, CancellationToken ct)
    {
        await _sender.Send(new AnalyzeVideoCommand(id), ct);
        return Ok(new { message = "AI analysis started in background.", videoId = id });
    }

    /// <summary>
    /// Trigger batch AI analysis for a list of video IDs.
    /// </summary>
    [HttpPost("videos/batch-analyze")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchAnalyzeVideos([FromBody] List<Guid> videoIds, CancellationToken ct)
    {
        var count = await _sender.Send(new BatchAnalyzeVideosCommand(videoIds), ct);
        return Ok(new { message = $"Batch AI analysis started for {count} videos.", count });
    }

    /// <summary>
    /// Fetch current model quota limits, priority order, and real-time RPM / RPD usage statistics.
    /// </summary>
    [HttpGet("quotas")]
    [ProducesResponseType(typeof(List<AiModelQuota>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AiModelQuota>>> GetQuotas(CancellationToken ct)
    {
        var quotas = await _sender.Send(new GetAiQuotasQuery(), ct);
        return Ok(quotas);
    }

    /// <summary>
    /// Trigger live provider model discovery and validation checks.
    /// </summary>
    [HttpPost("quotas/validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateQuotas(CancellationToken ct)
    {
        var updatedQuotas = await _sender.Send(new ValidateAiQuotasCommand(), ct);
        return Ok(new { message = "AI provider models validated and synced successfully.", quotas = updatedQuotas });
    }

    /// <summary>
    /// Reset all AI model rate limit counters and usage statistics.
    /// </summary>
    [HttpPost("quotas/reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetQuotas(CancellationToken ct)
    {
        await _sender.Send(new ResetAiQuotasCommand(), ct);
        return Ok(new { message = "All AI quota limits and usage counters have been reset." });
    }
}
