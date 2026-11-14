using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Videos;
using MyTube.Backend.Application.Features.Videos.Commands;
using MyTube.Backend.Application.Features.Videos.Queries;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize]
public class VideosController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMediaProcessor _mediaProcessor;

    public VideosController(ISender sender, IMediaProcessor mediaProcessor)
    {
        _sender = sender;
        _mediaProcessor = mediaProcessor;
    }

    private string GetBaseUrl()
    {
        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
        return $"{scheme}://{host}";
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VideoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VideoDto>>> GetVideos(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] string? sortBy,
        [FromQuery] string? mediaType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var baseUrl = GetBaseUrl();

        var query = new GetVideosQuery(userId, baseUrl, search, categoryId, sortBy, mediaType, page, pageSize);
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VideoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VideoDto>> GetVideoById(Guid id, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var baseUrl = GetBaseUrl();

        var video = await _sender.Send(new GetVideoByIdQuery(id, userId, baseUrl), ct);
        if (video == null)
        {
            return NotFound(new { message = "Video not found" });
        }

        return Ok(video);
    }

    [HttpGet("continue-watching")]
    [ProducesResponseType(typeof(List<VideoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VideoDto>>> GetContinueWatching([FromQuery] int limit = 6, CancellationToken ct = default)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var baseUrl = GetBaseUrl();

        var result = await _sender.Send(new GetContinueWatchingQuery(userId, baseUrl, limit), ct);
        return Ok(result);
    }

    [HttpGet("{id}/thumbnail")]
    [HttpHead("{id}/thumbnail")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVideoThumbnail(Guid id, CancellationToken ct)
    {
        var video = await _sender.Send(new GetVideoFileQuery(id), ct);
        if (video == null || string.IsNullOrEmpty(video.ThumbnailPath))
        {
            return NotFound();
        }

        var resolvedPath = _mediaProcessor.ResolvePhysicalFilePath(video.ThumbnailPath);
        if (!System.IO.File.Exists(resolvedPath))
        {
            return NotFound();
        }

        return PhysicalFile(resolvedPath, "image/jpeg");
    }

    [HttpGet("{id}/stream")]
    [HttpHead("{id}/stream")]
    [AllowAnonymous]
    public async Task<IActionResult> StreamVideo(Guid id, [FromQuery] double start = 0, [FromQuery] int? audioTrack = null, CancellationToken ct = default)
    {
        var video = await _sender.Send(new GetVideoFileQuery(id), ct);
        if (video == null)
        {
            return NotFound(new { message = "Video not found in database" });
        }

        var filePath = _mediaProcessor.ResolvePhysicalFilePath(video.FilePath);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = $"Video file does not exist on disk at path: {video.FilePath}" });
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        Response.Headers["Access-Control-Allow-Origin"] = "*";
        Response.Headers["Access-Control-Allow-Headers"] = "Range, Origin, Content-Type, Accept";
        Response.Headers["Access-Control-Expose-Headers"] = "Content-Length, Content-Range, Accept-Ranges, Content-Type";

        // If natively supported format, stream directly using physical ranges
        if ((ext == ".mp4" || ext == ".webm" || ext == ".ogg" || ext == ".mp3" || ext == ".m4a" || ext == ".opus" || ext == ".wav" || ext == ".flac" || ext == ".aac") && (!audioTrack.HasValue || audioTrack.Value == 0))
        {
            var contentType = ext switch
            {
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".ogg" => "video/ogg",
                ".mp3" => "audio/mpeg",
                ".m4a" => "audio/mp4",
                ".opus" => "audio/opus",
                ".wav" => "audio/wav",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                _ => "video/mp4"
            };
            return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
        }

        // Otherwise transcode fragmented MP4 on the fly
        Response.ContentType = "video/mp4";

        var startSecondsStr = start.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var audioMap = audioTrack.HasValue ? $"-map 0:a:{audioTrack.Value}" : "-map 0:a:0";

        var isWebCompatibleVideo = string.IsNullOrWhiteSpace(video.VideoCodec) ||
            video.VideoCodec.Contains("264", StringComparison.OrdinalIgnoreCase) ||
            video.VideoCodec.Contains("avc", StringComparison.OrdinalIgnoreCase) ||
            video.VideoCodec.Contains("265", StringComparison.OrdinalIgnoreCase) ||
            video.VideoCodec.Contains("hevc", StringComparison.OrdinalIgnoreCase) ||
            video.VideoCodec.Contains("vp9", StringComparison.OrdinalIgnoreCase) ||
            video.VideoCodec.Contains("vp09", StringComparison.OrdinalIgnoreCase) ||
            video.VideoCodec.Contains("av1", StringComparison.OrdinalIgnoreCase) ||
            video.VideoCodec.Contains("av01", StringComparison.OrdinalIgnoreCase) ||
            ext == ".mp4" || ext == ".webm" || ext == ".mkv";
        var vcodec = isWebCompatibleVideo ? "copy" : "libx264 -preset ultrafast -tune zerolatency -crf 26";

        var isAac = (!string.IsNullOrWhiteSpace(video.AudioCodec) &&
                     video.AudioCodec.Contains("aac", StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(video.FileName) &&
                        video.FileName.Contains("aac", StringComparison.OrdinalIgnoreCase));
        var acodec = isAac ? "copy" : "aac -b:a 192k";

        var hasVideo = (video.Width > 0 && video.Height > 0) ||
                       (!string.IsNullOrWhiteSpace(video.VideoCodec) && !video.VideoCodec.Equals("none", StringComparison.OrdinalIgnoreCase));
        var videoMap = hasVideo ? "-map 0:v:0" : "-vn";

        var args = start > 0
            ? $"-nostdin -ss {startSecondsStr} -i \"{filePath}\" {videoMap} {audioMap} -sn {(hasVideo ? $"-c:v {vcodec} " : "")}-c:a {acodec} -avoid_negative_ts make_zero -f mp4 -movflags frag_keyframe+empty_moov+default_base_moof pipe:1"
            : $"-nostdin -i \"{filePath}\" {videoMap} {audioMap} -sn {(hasVideo ? $"-c:v {vcodec} " : "")}-c:a {acodec} -f mp4 -movflags frag_keyframe+empty_moov+default_base_moof pipe:1";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        _ = Task.Run(async () =>
        {
            try
            {
                using var reader = process.StandardError;
                while (await reader.ReadLineAsync() != null) { }
            }
            catch { }
        });

        try
        {
            await process.StandardOutput.BaseStream.CopyToAsync(Response.Body, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(true); } catch { }
            }
            process.Dispose();
        }

        return new EmptyResult();
    }

    [HttpGet("{id}/download")]
    [HttpHead("{id}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadVideo(Guid id, CancellationToken ct)
    {
        var video = await _sender.Send(new GetVideoFileQuery(id), ct);
        if (video == null)
        {
            var downloadManager = HttpContext.RequestServices.GetService<IDownloadManager>();
            var job = downloadManager?.GetJobs().FirstOrDefault(j => j.Id == id);
            if (job != null && !string.IsNullOrWhiteSpace(job.FilePath))
            {
                var resolvedJobPath = _mediaProcessor.ResolvePhysicalFilePath(job.FilePath);
                if (System.IO.File.Exists(resolvedJobPath))
                {
                    var jobExt = Path.GetExtension(resolvedJobPath).ToLowerInvariant();
                    var jobMime = jobExt switch
                    {
                        ".mp4" => "video/mp4",
                        ".webm" => "video/webm",
                        ".mkv" => "video/x-matroska",
                        ".mp3" => "audio/mpeg",
                        ".m4a" => "audio/mp4",
                        ".opus" => "audio/opus",
                        ".wav" => "audio/wav",
                        _ => "application/octet-stream"
                    };
                    return PhysicalFile(resolvedJobPath, jobMime, fileDownloadName: Path.GetFileName(resolvedJobPath), enableRangeProcessing: true);
                }
            }

            return NotFound(new { message = "Video not found in database" });
        }

        var filePath = _mediaProcessor.ResolvePhysicalFilePath(video.FilePath);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = $"Video file does not exist on disk at path: {video.FilePath}" });
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".opus" => "audio/opus",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".aac" => "audio/aac",
            _ => "application/octet-stream"
        };

        var invalidChars = Path.GetInvalidFileNameChars();
        var safeTitle = string.Join("_", (video.DisplayTitle ?? "video").Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "video";
        var downloadFileName = safeTitle.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? safeTitle : $"{safeTitle}{ext}";

        return PhysicalFile(filePath, contentType, fileDownloadName: downloadFileName, enableRangeProcessing: true);
    }

    [HttpGet("{id}/subtitles/{trackIndex}")]
    [HttpHead("{id}/subtitles/{trackIndex}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSubtitleFile(Guid id, int trackIndex, CancellationToken ct)
    {
        var video = await _sender.Send(new GetVideoFileQuery(id), ct);
        if (video == null)
        {
            return NotFound(new { message = "Video not found in database" });
        }

        var filePath = _mediaProcessor.ResolvePhysicalFilePath(video.FilePath);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = $"Video file does not exist on disk at path: {video.FilePath}" });
        }

        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return NotFound(new { message = "Media directory does not exist" });
        }

        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var subFiles = Directory.GetFiles(dir, "*.*")
            .Where(f => (f.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                        && Path.GetFileName(f).StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Contains("-orig", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(f => f.Length)
            .ThenBy(f => f)
            .ToList();

        Response.Headers["Access-Control-Allow-Origin"] = "*";
        Response.Headers["Access-Control-Allow-Headers"] = "Range, Origin, Content-Type, Accept";

        if (trackIndex >= 0 && trackIndex < subFiles.Count)
        {
            var targetSubFile = subFiles[trackIndex];
            var ext = Path.GetExtension(targetSubFile).ToLowerInvariant();

            if (ext == ".vtt")
            {
                return PhysicalFile(targetSubFile, "text/vtt; charset=utf-8", enableRangeProcessing: true);
            }
            if (ext == ".srt")
            {
                var srtContent = await System.IO.File.ReadAllTextAsync(targetSubFile, ct);
                var vttContent = ConvertSrtToVtt(srtContent);
                return Content(vttContent, "text/vtt; charset=utf-8");
            }
        }

        return NotFound(new { message = "Subtitle track not found" });
    }

    private static string ConvertSrtToVtt(string srt)
    {
        if (string.IsNullOrWhiteSpace(srt)) return "WEBVTT\n\n";
        var vtt = System.Text.RegularExpressions.Regex.Replace(srt, @"(\d{2}:\d{2}:\d{2}),(\d{3})", "$1.$2");
        return "WEBVTT\n\n" + vtt.TrimStart();
    }

    [HttpGet("favorites")]
    [ProducesResponseType(typeof(List<VideoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VideoDto>>> GetFavorites(CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var baseUrl = GetBaseUrl();
        var favorites = await _sender.Send(new GetFavoritesQuery(userId, baseUrl), ct);
        return Ok(favorites);
    }

    [HttpPost("{id}/favorite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ToggleFavorite(Guid id, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var isFavorite = await _sender.Send(new ToggleFavoriteCommand(userId, id), ct);
        return Ok(new { isFavorite });
    }

    [HttpPut("{id}/progress")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateProgress(Guid id, [FromBody] ProgressUpdateRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        await _sender.Send(new UpdateProgressCommand(userId, id, request.PositionSeconds, request.DurationSeconds), ct);
        return NoContent();
    }

    [HttpGet("{id}/progress")]
    [ProducesResponseType(typeof(WatchProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchProgressDto>> GetProgress(Guid id, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var progress = await _sender.Send(new GetProgressQuery(userId, id), ct);
        if (progress == null)
        {
            return NotFound();
        }
        return Ok(progress);
    }

    [HttpPut("{id}/category")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateVideoCategory(Guid id, [FromBody] UpdateVideoCategoryRequest request, CancellationToken ct)
    {
        var success = await _sender.Send(new UpdateVideoCategoryCommand(id, request.CategoryId), ct);
        if (!success)
        {
            return BadRequest(new { message = "Failed to update category. Check if the video and category exist." });
        }
        return Ok(new { message = "Category updated successfully.", categoryId = request.CategoryId });
    }

    [HttpPost("delete-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteVideosBatch([FromBody] DeleteVideosRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new DeleteVideosBatchCommand(request.VideoIds), ct);
        return Ok(new { message = "Delete operation completed successfully.", deletedCount = result });
    }

    [HttpPut("category-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateVideosCategoryBatch([FromBody] UpdateVideosCategoryRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateVideosCategoryBatchCommand(request.VideoIds, request.CategoryId), ct);
        return Ok(new { message = "Category update completed.", updatedCount = result });
    }

    [HttpGet("{id}/qa")]
    [ProducesResponseType(typeof(List<VideoQaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VideoQaDto>>> GetVideoQaHistory(Guid id, CancellationToken ct)
    {
        var qas = await _sender.Send(new GetVideoQaHistoryQuery(id), ct);
        return Ok(qas);
    }

    [HttpPost("{id}/qa")]
    [ProducesResponseType(typeof(VideoQaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VideoQaDto>> AskVideoQuestion(Guid id, [FromBody] AskVideoQuestionRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var qa = await _sender.Send(new AskVideoQuestionCommand(id, userId, request.Question), ct);
        return Ok(qa);
    }
}

public sealed record AskVideoQuestionRequest
{
    public string Question { get; init; } = string.Empty;
}

public sealed record UpdateVideosCategoryRequest
{
    public List<Guid> VideoIds { get; init; } = new();
    public int CategoryId { get; init; }
}

public sealed record ProgressUpdateRequest
{
    public double PositionSeconds { get; init; }
    public double DurationSeconds { get; init; }
}

public sealed record UpdateVideoCategoryRequest
{
    public int CategoryId { get; init; }
}
