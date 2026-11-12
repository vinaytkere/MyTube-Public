using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public record ToolExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false,
    bool Cancelled = false);

public interface IMediaToolRunner
{
    string GetYtDlpPath();
    string GetFfmpegPath();
    string GetFfprobePath();

    Task<ToolExecutionResult> RunToolAsync(
        string toolName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        Action<string>? onStandardOutputLine = null,
        Action<string>? onStandardErrorLine = null,
        string? workingDirectory = null);
}
