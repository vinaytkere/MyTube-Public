using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application.Features.Sonarr;
using MyTube.Backend.Application.Features.Sonarr.Commands;
using MyTube.Backend.Application.Features.Sonarr.Queries;
using MyTube.Backend.Application.Sonarr;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize]
[Produces("application/json")]
public class SonarrController : ControllerBase
{
    private readonly ISender _sender;

    public SonarrController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(SonarrStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SonarrStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _sender.Send(new GetSonarrStatusQuery(), cancellationToken);
        return Ok(status);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<SonarrSeriesDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SonarrSeriesDto>>> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        var results = await _sender.Send(new SearchSonarrSeriesQuery(query), cancellationToken);
        return Ok(results);
    }

    [HttpGet("library")]
    [ProducesResponseType(typeof(List<SonarrSeriesDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SonarrSeriesDto>>> GetLibrary(CancellationToken cancellationToken)
    {
        var series = await _sender.Send(new GetSonarrLibraryQuery(), cancellationToken);
        return Ok(series);
    }

    [HttpGet("series/{id:int}")]
    [ProducesResponseType(typeof(SonarrSeriesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SonarrSeriesDto>> GetSeriesById(int id, CancellationToken cancellationToken)
    {
        var series = await _sender.Send(new GetSonarrSeriesByIdQuery(id), cancellationToken);
        if (series == null) return NotFound(new { message = "Series not found in Sonarr." });
        return Ok(series);
    }

    [HttpGet("series/{id:int}/episodes")]
    [ProducesResponseType(typeof(List<SonarrEpisodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SonarrEpisodeDto>>> GetEpisodes(int id, CancellationToken cancellationToken)
    {
        var episodes = await _sender.Send(new GetSonarrEpisodesQuery(id), cancellationToken);
        return Ok(episodes);
    }

    [HttpPost("add")]
    [ProducesResponseType(typeof(SonarrSeriesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SonarrSeriesDto>> AddSeries([FromBody] SonarrAddSeriesRequest request, CancellationToken cancellationToken)
    {
        var created = await _sender.Send(new AddSonarrSeriesCommand(request), cancellationToken);
        return Ok(created);
    }

    [HttpGet("releases")]
    [ProducesResponseType(typeof(List<SonarrReleaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SonarrReleaseDto>>> GetReleases([FromQuery] int seriesId, [FromQuery] int? episodeId = null, CancellationToken cancellationToken = default)
    {
        var releases = await _sender.Send(new GetSonarrReleasesQuery(seriesId, episodeId), cancellationToken);
        return Ok(releases);
    }

    [HttpPost("download-release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DownloadRelease([FromBody] SonarrDownloadReleaseRequest request, CancellationToken cancellationToken)
    {
        var success = await _sender.Send(new DownloadSonarrReleaseCommand(request), cancellationToken);
        if (success)
            return Ok(new { success = true, message = "Episode release sent to download client." });

        return BadRequest(new { success = false, message = "Sonarr rejected the release download." });
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(List<SonarrQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SonarrQueueItemDto>>> GetQueue(CancellationToken cancellationToken)
    {
        var queue = await _sender.Send(new GetSonarrQueueQuery(), cancellationToken);
        return Ok(queue);
    }

    [HttpGet("qualityprofiles")]
    [ProducesResponseType(typeof(List<SonarrQualityProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SonarrQualityProfileDto>>> GetQualityProfiles(CancellationToken cancellationToken)
    {
        var profiles = await _sender.Send(new GetSonarrQualityProfilesQuery(), cancellationToken);
        return Ok(profiles);
    }

    [HttpGet("rootfolders")]
    [ProducesResponseType(typeof(List<SonarrRootFolderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SonarrRootFolderDto>>> GetRootFolders(CancellationToken cancellationToken)
    {
        var folders = await _sender.Send(new GetSonarrRootFoldersQuery(), cancellationToken);
        return Ok(folders);
    }

    [HttpPost("test")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(SonarrStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SonarrStatusDto>> TestConnection([FromBody] SonarrTestConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new TestSonarrConnectionCommand(request), cancellationToken);
        return Ok(result);
    }

    [HttpGet("settings")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(SonarrSettingsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SonarrSettingsResponseDto>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _sender.Send(new GetSonarrSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPost("settings")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] SonarrSettings settings, CancellationToken cancellationToken)
    {
        await _sender.Send(new UpdateSonarrSettingsCommand(settings), cancellationToken);
        return Ok(new { success = true, message = "Sonarr settings updated successfully." });
    }
}
