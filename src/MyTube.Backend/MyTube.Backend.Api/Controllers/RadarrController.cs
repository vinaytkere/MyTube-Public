using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application.Features.Radarr;
using MyTube.Backend.Application.Features.Radarr.Commands;
using MyTube.Backend.Application.Features.Radarr.Queries;
using MyTube.Backend.Application.Prowlarr;
using MyTube.Backend.Application.Radarr;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize]
[Produces("application/json")]
public class RadarrController : ControllerBase
{
    private readonly ISender _sender;

    public RadarrController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(RadarrStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RadarrStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _sender.Send(new GetRadarrStatusQuery(), cancellationToken);
        return Ok(status);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<RadarrMovieDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RadarrMovieDto>>> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        var results = await _sender.Send(new SearchRadarrMoviesQuery(query), cancellationToken);
        return Ok(results);
    }

    [HttpGet("library")]
    [ProducesResponseType(typeof(List<RadarrMovieDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RadarrMovieDto>>> GetLibrary(CancellationToken cancellationToken)
    {
        var movies = await _sender.Send(new GetRadarrLibraryQuery(), cancellationToken);
        return Ok(movies);
    }

    [HttpPost("add")]
    [ProducesResponseType(typeof(RadarrMovieDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RadarrMovieDto>> AddMovie([FromBody] RadarrAddMovieRequest request, CancellationToken cancellationToken)
    {
        var created = await _sender.Send(new AddRadarrMovieCommand(request), cancellationToken);
        return Ok(created);
    }

    [HttpPost("auto-grab")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AutoGrab([FromBody] RadarrAddMovieRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AutoGrabRadarrMovieCommand(request), cancellationToken);
        return Ok(result);
    }

    [HttpGet("releases")]
    [ProducesResponseType(typeof(List<RadarrReleaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RadarrReleaseDto>>> GetReleases([FromQuery] int movieId, CancellationToken cancellationToken)
    {
        var releases = await _sender.Send(new GetRadarrReleasesQuery(movieId), cancellationToken);
        return Ok(releases);
    }

    [HttpPost("download-release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DownloadRelease([FromBody] RadarrDownloadReleaseRequest request, CancellationToken cancellationToken)
    {
        var success = await _sender.Send(new DownloadRadarrReleaseCommand(request), cancellationToken);
        if (success)
            return Ok(new { success = true, message = "Release sent to download client." });

        return BadRequest(new { success = false, message = "Radarr rejected the release download." });
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(List<RadarrQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RadarrQueueItemDto>>> GetQueue(CancellationToken cancellationToken)
    {
        var queue = await _sender.Send(new GetRadarrQueueQuery(), cancellationToken);
        return Ok(queue);
    }

    [HttpDelete("queue/{id}")]
    public async Task<IActionResult> DeleteQueueItem(
        int id, 
        [FromQuery] int? transmissionId = null, 
        [FromQuery] bool deleteData = true, 
        CancellationToken cancellationToken = default)
    {
        var effTxId = transmissionId ?? (id > 0 ? id : (int?)null);
        var success = await _sender.Send(new CancelRadarrQueueItemCommand(id, effTxId, deleteData), cancellationToken);
        if (success)
        {
            return Ok(new { success = true, message = "Download cancelled and removed." });
        }
        return BadRequest(new { success = false, message = "Could not cancel download." });
    }

    [HttpPost("queue/{id}/pause")]
    public async Task<IActionResult> PauseQueueItem(
        int id,
        [FromQuery] int? transmissionId = null,
        CancellationToken cancellationToken = default)
    {
        var effTxId = transmissionId ?? (id > 0 ? id : (int?)null);
        var success = await _sender.Send(new PauseRadarrQueueItemCommand(id, effTxId), cancellationToken);
        if (success)
        {
            return Ok(new { success = true, message = "Download paused." });
        }
        return BadRequest(new { success = false, message = "Could not pause download." });
    }

    [HttpPost("queue/{id}/resume")]
    public async Task<IActionResult> ResumeQueueItem(
        int id,
        [FromQuery] int? transmissionId = null,
        CancellationToken cancellationToken = default)
    {
        var effTxId = transmissionId ?? (id > 0 ? id : (int?)null);
        var success = await _sender.Send(new ResumeRadarrQueueItemCommand(id, effTxId), cancellationToken);
        if (success)
        {
            return Ok(new { success = true, message = "Download resumed." });
        }
        return BadRequest(new { success = false, message = "Could not resume download." });
    }

    [HttpPost("queue/{id}/kickstart")]
    public async Task<IActionResult> KickstartQueueItem(
        int id,
        [FromQuery] int? transmissionId = null,
        CancellationToken cancellationToken = default)
    {
        var effTxId = transmissionId ?? (id > 0 ? id : (int?)null);
        var success = await _sender.Send(new KickstartRadarrQueueItemCommand(id, effTxId), cancellationToken);
        if (success)
        {
            return Ok(new { success = true, message = "Injected 15+ tier-1 trackers and re-announced torrent to all seeders." });
        }
        return BadRequest(new { success = false, message = "Could not kickstart torrent." });
    }

    [HttpGet("qualityprofiles")]
    [ProducesResponseType(typeof(List<RadarrQualityProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RadarrQualityProfileDto>>> GetQualityProfiles(CancellationToken cancellationToken)
    {
        var profiles = await _sender.Send(new GetRadarrQualityProfilesQuery(), cancellationToken);
        return Ok(profiles);
    }

    [HttpGet("rootfolders")]
    [ProducesResponseType(typeof(List<RadarrRootFolderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RadarrRootFolderDto>>> GetRootFolders(CancellationToken cancellationToken)
    {
        var folders = await _sender.Send(new GetRadarrRootFoldersQuery(), cancellationToken);
        return Ok(folders);
    }

    [HttpPost("test")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(RadarrStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RadarrStatusDto>> TestConnection([FromBody] RadarrTestConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new TestRadarrConnectionCommand(request), cancellationToken);
        return Ok(result);
    }

    [HttpGet("settings")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(RadarrSettingsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RadarrSettingsResponseDto>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _sender.Send(new GetRadarrSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    [HttpPost("settings")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] RadarrSettings settings, CancellationToken cancellationToken)
    {
        await _sender.Send(new UpdateRadarrSettingsCommand(settings), cancellationToken);
        return Ok(new { success = true, message = "Radarr settings updated successfully." });
    }

    [HttpGet("indexers/search")]
    [ProducesResponseType(typeof(List<ProwlarrSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProwlarrSearchResultDto>>> SearchIndexers([FromQuery] string query, [FromQuery] string? category = null, CancellationToken cancellationToken = default)
    {
        var results = await _sender.Send(new SearchProwlarrIndexersQuery(query, category), cancellationToken);
        return Ok(results);
    }

    [HttpPost("indexers/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DownloadIndexerRelease([FromBody] ProwlarrDownloadRequest request, CancellationToken cancellationToken)
    {
        var success = await _sender.Send(new DownloadProwlarrReleaseCommand(request), cancellationToken);
        if (success)
            return Ok(new { success = true, message = "Torrent successfully dispatched to VPN download client." });

        return BadRequest(new { success = false, message = "Download client rejected torrent." });
    }

    [HttpGet("downloads/summary")]
    [ProducesResponseType(typeof(RadarrDownloadSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RadarrDownloadSummaryDto>> GetDownloadSummary(CancellationToken cancellationToken)
    {
        var summary = await _sender.Send(new GetRadarrDownloadSummaryQuery(), cancellationToken);
        return Ok(summary);
    }

    [HttpPost("sync-links")]
    public async Task<IActionResult> SyncLinks(CancellationToken cancellationToken)
    {
        var success = await _sender.Send(new SyncRadarrLinksCommand(), cancellationToken);
        if (success)
        {
            return Ok(new { success = true, message = "Radarr media links synchronized into separate non-deletable folder." });
        }
        return StatusCode(500, new { success = false, message = "Could not sync Radarr media links." });
    }

    [HttpGet("movies/{id}/download")]
    [HttpHead("movies/{id}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadMovieFile(int id, CancellationToken cancellationToken)
    {
        var fileInfo = await _sender.Send(new GetRadarrMovieDownloadQuery(id), cancellationToken);
        if (fileInfo == null || !System.IO.File.Exists(fileInfo.FilePath))
        {
            return NotFound(new { message = "Movie file not found on disk or in Radarr." });
        }

        return PhysicalFile(
            fileInfo.FilePath,
            fileInfo.ContentType,
            fileDownloadName: fileInfo.FileName,
            enableRangeProcessing: true);
    }

    [HttpGet("files/download")]
    [HttpHead("files/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadGenericFile([FromQuery] string path, [FromQuery] bool zip = false, CancellationToken cancellationToken = default)
    {
        if (HttpMethods.IsHead(Request.Method))
            return Ok();

        var target = await _sender.Send(new GetRadarrTargetDownloadQuery(path), cancellationToken);
        if (target == null)
        {
            return NotFound(new { message = "Requested file or folder not found on disk." });
        }

        if (target.IsDirectory || zip)
        {
            if (Directory.Exists(target.ResolvedPath))
            {
                await StreamDirectoryAsZipAsync(target.ResolvedPath, target.FileName, cancellationToken);
                return new EmptyResult();
            }
            else if (System.IO.File.Exists(target.ResolvedPath))
            {
                await StreamSingleFileAsZipAsync(target.ResolvedPath, cancellationToken);
                return new EmptyResult();
            }
        }

        return PhysicalFile(
            target.ResolvedPath,
            target.ContentType,
            fileDownloadName: target.FileName,
            enableRangeProcessing: true);
    }

    [HttpGet("torrents/{transmissionId}/download")]
    [HttpHead("torrents/{transmissionId}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadTorrent(
        int transmissionId,
        [FromQuery] int? fileIndex = null,
        [FromQuery] int? movieId = null,
        [FromQuery] bool zip = false,
        CancellationToken cancellationToken = default)
    {
        if (HttpMethods.IsHead(Request.Method))
            return Ok();

        var info = await _sender.Send(new GetRadarrTorrentDownloadQuery(transmissionId, fileIndex, movieId), cancellationToken);
        if (info == null || (string.IsNullOrEmpty(info.TargetPath) && (info.Files == null || info.Files.Count == 0)))
        {
            var msg = !string.IsNullOrWhiteSpace(info?.ErrorMessage)
                ? info.ErrorMessage
                : "Torrent download not found or files not yet available on disk.";
            return NotFound(new { message = msg });
        }

        if (info.IsDirectory || zip)
        {
            if (!string.IsNullOrEmpty(info.TargetPath) && Directory.Exists(info.TargetPath))
            {
                await StreamDirectoryAsZipAsync(info.TargetPath, info.DownloadName, cancellationToken);
                return new EmptyResult();
            }
            else if (info.Files != null && info.Files.Count > 0)
            {
                await StreamFileListAsZipAsync(info.Files, info.DownloadName, cancellationToken);
                return new EmptyResult();
            }
        }

        if (!string.IsNullOrEmpty(info.TargetPath) && System.IO.File.Exists(info.TargetPath))
        {
            var fileName = Path.GetFileName(info.TargetPath);
            return PhysicalFile(
                info.TargetPath,
                "application/octet-stream",
                fileDownloadName: fileName,
                enableRangeProcessing: true);
        }

        return NotFound(new { message = "Requested file not found on disk." });
    }

    [HttpGet("movies/{id}/poster/download")]
    [HttpHead("movies/{id}/poster/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadPoster(int id, CancellationToken cancellationToken)
    {
        var poster = await _sender.Send(new GetRadarrPosterDownloadQuery(id), cancellationToken);
        if (poster == null || poster.ImageBytes == null || poster.ImageBytes.Length == 0)
        {
            return NotFound(new { message = "Poster image not found." });
        }

        return File(poster.ImageBytes, poster.ContentType, fileDownloadName: poster.FileName);
    }

    [HttpGet("movies/{id}/fanart/download")]
    [HttpHead("movies/{id}/fanart/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFanart(int id, CancellationToken cancellationToken)
    {
        var fanart = await _sender.Send(new GetRadarrFanartDownloadQuery(id), cancellationToken);
        if (fanart == null || fanart.ImageBytes == null || fanart.ImageBytes.Length == 0)
        {
            return NotFound(new { message = "Fanart image not found." });
        }

        return File(fanart.ImageBytes, fanart.ContentType, fileDownloadName: fanart.FileName);
    }

    [HttpDelete("movies/{id}/files")]
    public async Task<IActionResult> DeleteMovieFiles(int id, CancellationToken cancellationToken)
    {
        var success = await _sender.Send(new DeleteRadarrMovieFilesCommand(id), cancellationToken);
        if (success)
        {
            return Ok(new { success = true, message = "Downloaded files deleted successfully from disk and Radarr." });
        }
        return BadRequest(new { success = false, message = "Could not delete movie files." });
    }

    [HttpPost("files/delete-batch")]
    public async Task<IActionResult> BatchDeleteFiles([FromBody] BatchDeleteFilesRequest request, CancellationToken cancellationToken)
    {
        var deletedCount = await _sender.Send(new BatchDeleteRadarrFilesCommand(request.MovieIds, request.FilePaths), cancellationToken);
        return Ok(new { success = true, deletedCount, message = $"Successfully deleted {deletedCount} items from disk." });
    }

    [HttpDelete("downloads/all")]
    public async Task<IActionResult> DeleteAllDownloads(CancellationToken cancellationToken)
    {
        var deletedCount = await _sender.Send(new DeleteAllRadarrDownloadsCommand(), cancellationToken);
        return Ok(new
        {
            success = true,
            deletedCount,
            message = $"Successfully deleted all downloaded files from {deletedCount} movies. Non-deletable folder was preserved."
        });
    }

    private async Task StreamDirectoryAsZipAsync(string directoryPath, string archiveName, CancellationToken cancellationToken)
    {
        var syncIo = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIo != null)
        {
            syncIo.AllowSynchronousIO = true;
        }

        var cleanName = SanitizeArchiveName(archiveName);
        if (!cleanName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            cleanName += ".zip";

        Response.ContentType = "application/zip";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{cleanName}\"");

        using var archive = new ZipArchive(Response.Body, ZipArchiveMode.Create, leaveOpen: true);
        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relPath = Path.GetRelativePath(directoryPath, file).Replace('\\', '/');
            var entry = archive.CreateEntry(relPath, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await fileStream.CopyToAsync(entryStream, cancellationToken);
        }
    }

    private async Task StreamFileListAsZipAsync(List<string> files, string archiveName, CancellationToken cancellationToken)
    {
        var syncIo = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIo != null)
        {
            syncIo.AllowSynchronousIO = true;
        }

        var cleanName = SanitizeArchiveName(archiveName);
        if (!cleanName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            cleanName += ".zip";

        Response.ContentType = "application/zip";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{cleanName}\"");

        using var archive = new ZipArchive(Response.Body, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!System.IO.File.Exists(file)) continue;

            var fileName = Path.GetFileName(file);
            var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await fileStream.CopyToAsync(entryStream, cancellationToken);
        }
    }

    private async Task StreamSingleFileAsZipAsync(string filePath, CancellationToken cancellationToken)
    {
        var syncIo = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIo != null)
        {
            syncIo.AllowSynchronousIO = true;
        }

        var fileName = Path.GetFileName(filePath);
        var cleanName = SanitizeArchiveName(fileName) + ".zip";

        Response.ContentType = "application/zip";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{cleanName}\"");

        using var archive = new ZipArchive(Response.Body, ZipArchiveMode.Create, leaveOpen: true);
        var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await fileStream.CopyToAsync(entryStream, cancellationToken);
    }

    private static string SanitizeArchiveName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "download";
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "download" : clean;
    }
}
