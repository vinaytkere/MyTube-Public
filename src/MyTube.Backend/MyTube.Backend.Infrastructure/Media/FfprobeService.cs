using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public class FfprobeService : IMediaProbeService
{
    private readonly IMediaToolRunner _toolRunner;
    private readonly ISourceMetadataService _sourceMetadataService;
    private readonly ILogger<FfprobeService> _logger;

    public FfprobeService(
        IMediaToolRunner toolRunner,
        ISourceMetadataService sourceMetadataService,
        ILogger<FfprobeService> logger)
    {
        _toolRunner = toolRunner;
        _sourceMetadataService = sourceMetadataService;
        _logger = logger;
    }

    public async Task<MediaProbeResult> ProbeFileAsync(string path, CancellationToken token = default)
    {
        var res = new MediaProbeResult();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            res.ErrorMessage = "File does not exist or path is invalid.";
            return res;
        }

        try
        {
            res.FileSize = new FileInfo(path).Length;
            var probeArgs = new[]
            {
                "-v", "error",
                "-show_streams",
                "-show_format",
                "-of", "json",
                path
            };

            var toolRes = await _toolRunner.RunToolAsync("ffprobe", probeArgs, token);
            res.ExitCode = toolRes.ExitCode;
            if (toolRes.ExitCode != 0 || string.IsNullOrWhiteSpace(toolRes.StandardOutput))
            {
                res.ErrorMessage = !string.IsNullOrWhiteSpace(toolRes.StandardError)
                    ? toolRes.StandardError.Trim()
                    : $"ffprobe exited with code {toolRes.ExitCode}";
                _logger.LogWarning("ffprobe exited with code {Code} for {Path}: {Stderr}", toolRes.ExitCode, path, toolRes.StandardError);
                return res;
            }

            using var doc = JsonDocument.Parse(toolRes.StandardOutput);
            var root = doc.RootElement;
            res.IsReadable = true;

            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                res.StreamCount = streams.GetArrayLength();

                foreach (var stream in streams.EnumerateArray())
                {
                    if (!stream.TryGetProperty("codec_type", out var typeEl)) continue;
                    var codecType = typeEl.GetString();

                    if (string.Equals(codecType, "video", StringComparison.OrdinalIgnoreCase))
                    {
                        res.HasVideo = true;
                        res.VideoTrackCount++;

                        if (string.IsNullOrEmpty(res.VideoCodec) && stream.TryGetProperty("codec_name", out var vc))
                            res.VideoCodec = vc.GetString();

                        if (stream.TryGetProperty("width", out var w)) res.Width = w.GetInt32();
                        if (stream.TryGetProperty("height", out var h)) res.Height = h.GetInt32();
                        if (stream.TryGetProperty("pix_fmt", out var pf)) res.PixelFormat = pf.GetString();
                        if (stream.TryGetProperty("time_base", out var tb)) res.TimeBase = tb.GetString();

                        if (stream.TryGetProperty("r_frame_rate", out var fpsStr))
                        {
                            var fpsParts = fpsStr.GetString()?.Split('/');
                            if (fpsParts?.Length == 2 &&
                                double.TryParse(fpsParts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
                                double.TryParse(fpsParts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den) &&
                                den > 0)
                            {
                                res.FrameRate = num / den;
                            }
                        }
                    }
                    else if (string.Equals(codecType, "audio", StringComparison.OrdinalIgnoreCase))
                    {
                        res.HasAudio = true;
                        res.AudioTrackCount++;

                        if (string.IsNullOrEmpty(res.AudioCodec) && stream.TryGetProperty("codec_name", out var ac))
                            res.AudioCodec = ac.GetString();

                        if (stream.TryGetProperty("sample_rate", out var sr))
                        {
                            if (sr.ValueKind == JsonValueKind.String && int.TryParse(sr.GetString(), out var sRate))
                                res.SampleRate = sRate;
                            else if (sr.ValueKind == JsonValueKind.Number && sr.TryGetInt32(out var sRateNum))
                                res.SampleRate = sRateNum;
                        }

                        if (stream.TryGetProperty("channels", out var ch))
                            res.ChannelCount = ch.GetInt32();

                        if (stream.TryGetProperty("channel_layout", out var cl))
                            res.ChannelLayout = cl.GetString();
                    }

                    if (res.Duration == TimeSpan.Zero && stream.TryGetProperty("duration", out var streamDur))
                    {
                        if (streamDur.ValueKind == JsonValueKind.String &&
                            double.TryParse(streamDur.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sSec))
                        {
                            res.Duration = TimeSpan.FromSeconds(sSec);
                        }
                        else if (streamDur.ValueKind == JsonValueKind.Number && streamDur.TryGetDouble(out var sSecNum))
                        {
                            res.Duration = TimeSpan.FromSeconds(sSecNum);
                        }
                    }
                }
            }

            if (root.TryGetProperty("format", out var fmt))
            {
                if (fmt.TryGetProperty("format_name", out var fn)) res.ContainerFormat = fn.GetString();
                if (fmt.TryGetProperty("duration", out var dur))
                {
                    if (dur.ValueKind == JsonValueKind.String &&
                        double.TryParse(dur.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var durSeconds))
                    {
                        res.Duration = TimeSpan.FromSeconds(durSeconds);
                    }
                    else if (dur.ValueKind == JsonValueKind.Number && dur.TryGetDouble(out var durSecNum))
                    {
                        res.Duration = TimeSpan.FromSeconds(durSecNum);
                    }
                }
            }

            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while probing media file {Path}", path);
            res.ErrorMessage = ex.Message;
            return res;
        }
    }

    public Task<VideoProbeResultDto> ProbeUrlAsync(string url, CancellationToken token = default)
    {
        return _sourceMetadataService.ProbeRemoteUrlAsync(url, token);
    }

    public Task<PlaylistProbeResultDto> ProbePlaylistAsync(string url, CancellationToken token = default)
    {
        return _sourceMetadataService.ProbePlaylistAsync(url, token);
    }
}
