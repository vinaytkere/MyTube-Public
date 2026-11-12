using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application.Features.MediaOrganizer.Commands;
using MyTube.Backend.Application.Features.MediaOrganizer.Queries;
using MyTube.Backend.Application.MediaOrganizer.Dtos;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
[Produces("application/json")]
public class MediaOrganizerController : ControllerBase
{
    private readonly ISender _sender;
    private readonly MyTube.Backend.Application.MediaOrganizer.IMediaOrganizerService _organizerService;

    public MediaOrganizerController(
        ISender sender,
        MyTube.Backend.Application.MediaOrganizer.IMediaOrganizerService organizerService)
    {
        _sender = sender;
        _organizerService = organizerService;
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(MediaOrganizerSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaOrganizerSettingsDto>> GetSettings(CancellationToken ct)
    {
        var settings = await _sender.Send(new GetOrganizerSettingsQuery(), ct);
        return Ok(settings);
    }

    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] MediaOrganizerSettingsDto settings, CancellationToken ct)
    {
        await _sender.Send(new UpdateOrganizerSettingsCommand(settings), ct);
        return Ok(new { success = true, message = "Media organizer settings updated successfully." });
    }

    [HttpPost("preview/{videoId:guid}")]
    [ProducesResponseType(typeof(MediaRenamePreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaRenamePreviewDto>> PreviewOrganization(Guid videoId, CancellationToken ct)
    {
        var preview = await _sender.Send(new PreviewMediaRenamingQuery(videoId), ct);
        return Ok(preview);
    }

    [HttpPost("batch-preview")]
    [ProducesResponseType(typeof(List<MediaRenamePreviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MediaRenamePreviewDto>>> BatchPreview([FromBody] BatchOrganizeRequest? request, CancellationToken ct)
    {
        var previews = await _organizerService.BatchPreviewAsync(request?.VideoIds, ct);
        return Ok(previews);
    }

    [HttpPost("organize/{videoId:guid}")]
    [ProducesResponseType(typeof(MediaOrganizeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MediaOrganizeResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MediaOrganizeResultDto>> OrganizeMedia(Guid videoId, CancellationToken ct)
    {
        var result = await _sender.Send(new OrganizeMediaCommand(videoId), ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(List<MediaOrganizeResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MediaOrganizeResultDto>>> BatchOrganize([FromBody] BatchOrganizeRequest? request, CancellationToken ct)
    {
        var results = await _sender.Send(new BatchOrganizeMediaCommand(request?.VideoIds), ct);
        return Ok(results);
    }

    [HttpPost("consolidate")]
    [ProducesResponseType(typeof(CategoryConsolidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryConsolidationResultDto>> ConsolidateCategories(CancellationToken ct)
    {
        var result = await _organizerService.ConsolidateCategoriesAsync(ct);
        return Ok(result);
    }

    [HttpGet("audit")]
    [ProducesResponseType(typeof(MediaAuditReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaAuditReportDto>> GetAuditReport(CancellationToken ct)
    {
        var report = await _organizerService.GetAuditReportAsync(ct);
        return Ok(report);
    }
}

public sealed record BatchOrganizeRequest
{
    public List<Guid>? VideoIds { get; init; }
}
