using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure.Media;

public interface IFfmpegRunner
{
    Task<ToolExecutionResult> RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        Action<TimeSpan>? onProgressTimestamp = null,
        Action<string>? onStandardError = null,
        string? workingDirectory = null);
}

public class FfmpegRunner : IFfmpegRunner
{
    private readonly IMediaToolRunner _toolRunner;
    private readonly ILogger<FfmpegRunner> _logger;
    private static readonly Regex TimeRegex = new(@"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})", RegexOptions.Compiled);

    public FfmpegRunner(IMediaToolRunner toolRunner, ILogger<FfmpegRunner> logger)
    {
        _toolRunner = toolRunner;
        _logger = logger;
    }

    public async Task<ToolExecutionResult> RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        Action<TimeSpan>? onProgressTimestamp = null,
        Action<string>? onStandardError = null,
        string? workingDirectory = null)
    {
        void StderrHandler(string line)
        {
            onStandardError?.Invoke(line);

            if (onProgressTimestamp != null)
            {
                var match = TimeRegex.Match(line);
                if (match.Success &&
                    int.TryParse(match.Groups[1].Value, out var h) &&
                    int.TryParse(match.Groups[2].Value, out var m) &&
                    int.TryParse(match.Groups[3].Value, out var s) &&
                    int.TryParse(match.Groups[4].Value, out var cs))
                {
                    var ts = new TimeSpan(0, h, m, s, cs * 10);
                    onProgressTimestamp(ts);
                }
            }
        }

        return await _toolRunner.RunToolAsync(
            "ffmpeg",
            arguments,
            cancellationToken,
            timeout,
            onStandardOutputLine: null,
            onStandardErrorLine: StderrHandler,
            workingDirectory: workingDirectory);
    }
}
