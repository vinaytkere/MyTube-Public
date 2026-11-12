using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Iptv.Commands;
using MyTube.Backend.Application.Features.Iptv.Queries;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class IptvController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ISsrfValidator _ssrfValidator;
    private readonly IIptvStreamRelayService _streamRelayService;

    public IptvController(
        ISender sender, 
        ISsrfValidator ssrfValidator,
        IIptvStreamRelayService streamRelayService)
    {
        _sender = sender;
        _ssrfValidator = ssrfValidator;
        _streamRelayService = streamRelayService;
    }

    private string? GetCurrentUserId()
    {
        return User.Identity?.IsAuthenticated == true
            ? (User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity.Name)
            : null;
    }

    [HttpGet("channels")]
    [ProducesResponseType(typeof(PagedResult<IptvChannelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IptvChannelDto>>> GetChannels(
        [FromQuery] string? scope,
        [FromQuery] string? regionCode,
        [FromQuery] string? countryCode,
        [FromQuery] string? languageCode,
        [FromQuery] string? categoryId,
        [FromQuery] string? search,
        [FromQuery] bool favoritesOnly = false,
        [FromQuery] bool historyOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] bool httpsOnly = false,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();

        var query = new GetIptvChannelsQuery(
            UserId: userId,
            Scope: scope,
            RegionCode: regionCode,
            CountryCode: countryCode,
            LanguageCode: languageCode,
            CategoryId: categoryId,
            Search: search,
            FavoritesOnly: favoritesOnly,
            HistoryOnly: historyOnly,
            Page: page,
            PageSize: pageSize,
            HttpsOnly: httpsOnly);

        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("playlist.m3u")]
    [HttpGet("playlist")]
    [Produces("application/x-mpegurl")]
    public async Task<IActionResult> GetPlaylist(
        [FromQuery] bool favoritesOnly = false,
        [FromQuery] string? countryCode = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] string? languageCode = null,
        [FromQuery] bool httpsOnly = false,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var query = new GetIptvPlaylistM3uQuery(userId, favoritesOnly, countryCode, categoryId, languageCode, httpsOnly);
        var m3uContent = await _sender.Send(query, ct);

        var filename = favoritesOnly ? "mytube-favorites.m3u" : "mytube-iptv.m3u";
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{filename}\"");
        return Content(m3uContent, "application/x-mpegurl", Encoding.UTF8);
    }

    [HttpGet("stream-relay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StreamRelay(
        [FromQuery] string url,
        [FromQuery] string? userAgent = null,
        [FromQuery] string? referrer = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { message = "Query parameter 'url' is required." });
        }

        var result = await _streamRelayService.RelayStreamAsync(url, userAgent, referrer, ct);
        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        if (result.IsManifest)
        {
            return Content(result.ManifestContent!, result.ContentType, Encoding.UTF8);
        }

        if (result.StreamBody != null)
        {
            return File(result.StreamBody, result.ContentType, enableRangeProcessing: true);
        }

        return NoContent();
    }

    [HttpGet("channels/{id}")]
    [ProducesResponseType(typeof(IptvChannelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IptvChannelDto>> GetChannelById(
        string id,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _sender.Send(new GetIptvChannelByIdQuery(id, userId), ct);
        if (result == null)
        {
            return NotFound(new { message = $"Channel '{id}' not found." });
        }
        return Ok(result);
    }

    [HttpGet("filters")]
    [ProducesResponseType(typeof(IptvFilterOptionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IptvFilterOptionsDto>> GetFilters(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetIptvFiltersQuery(), ct);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("channels/{channelId}/favorite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> ToggleFavorite(
        string channelId,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var isFavorite = await _sender.Send(new ToggleIptvFavoriteCommand(userId, channelId), ct);
        return Ok(new { channelId, isFavorite });
    }

    [Authorize]
    [HttpGet("favorites")]
    [ProducesResponseType(typeof(PagedResult<IptvChannelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IptvChannelDto>>> GetFavorites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new GetIptvChannelsQuery(
            UserId: userId,
            Scope: null,
            RegionCode: null,
            CountryCode: null,
            LanguageCode: null,
            CategoryId: null,
            Search: null,
            FavoritesOnly: true,
            HistoryOnly: false,
            Page: page,
            PageSize: pageSize), ct);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("channels/{channelId}/watch")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RecordWatch(
        string channelId,
        [FromBody] RecordWatchRequest? request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _sender.Send(new RecordIptvWatchCommand(userId, channelId, request?.StreamId), ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResult<IptvChannelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IptvChannelDto>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new GetIptvChannelsQuery(
            UserId: userId,
            Scope: null,
            RegionCode: null,
            CountryCode: null,
            LanguageCode: null,
            CategoryId: null,
            Search: null,
            FavoritesOnly: false,
            HistoryOnly: true,
            Page: page,
            PageSize: pageSize), ct);

        return Ok(result);
    }

    [HttpPost("streams/{streamId}/report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReportStreamHealth(
        int streamId,
        [FromBody] ReportStreamHealthRequest request,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new ReportIptvStreamHealthCommand(streamId, request.IsWorking, request.ErrorMessage), ct);
        return Ok(new { streamId, updated = result });
    }

    [Authorize]
    [HttpPost("sync")]
    [ProducesResponseType(typeof(IptvSyncStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IptvSyncStatusDto>> TriggerSync(
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new SyncIptvCatalogCommand(force), ct);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("sync/status")]
    [ProducesResponseType(typeof(IptvSyncStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IptvSyncStatusDto>> GetSyncStatus(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetIptvSyncStatusQuery(), ct);
        return Ok(result);
    }

    [HttpPost("validate-url")]
    [ProducesResponseType(typeof(SsrfValidationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SsrfValidationResult>> ValidateStreamUrl(
        [FromBody] ValidateUrlRequest request,
        CancellationToken ct = default)
    {
        var result = await _ssrfValidator.ValidateUrlAsync(request.Url, ct);
        return Ok(result);
    }
}

public record RecordWatchRequest(int? StreamId);
public record ReportStreamHealthRequest(bool IsWorking, string? ErrorMessage);
public record ValidateUrlRequest(string Url);

