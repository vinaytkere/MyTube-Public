using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure;

public class MediaToolRunner : IMediaToolRunner
{
    private readonly ILogger<MediaToolRunner> _logger;
    private readonly string _ytDlpPath;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public MediaToolRunner(IConfiguration configuration, ILogger<MediaToolRunner> logger)
    {
        _logger = logger;

        _ytDlpPath = ResolveExecutablePath(
            configuration["Media:YtDlpPath"], 
            "yt-dlp", 
            "/usr/local/bin/yt-dlp", 
            "/usr/bin/yt-dlp");

        _ffmpegPath = ResolveExecutablePath(
            configuration["Media:FfmpegPath"], 
            "ffmpeg", 
            "/usr/local/bin/ffmpeg", 
            "/usr/bin/ffmpeg");

        _ffprobePath = ResolveExecutablePath(
            configuration["Media:FfprobePath"], 
            "ffprobe", 
            "/usr/local/bin/ffprobe", 
            "/usr/bin/ffprobe");

        _logger.LogInformation("MediaToolRunner initialized with tools: yt-dlp={YtDlp}, ffmpeg={Ffmpeg}, ffprobe={Ffprobe}",
            _ytDlpPath, _ffmpegPath, _ffprobePath);
    }

    public string GetYtDlpPath() => _ytDlpPath;
    public string GetFfmpegPath() => _ffmpegPath;
    public string GetFfprobePath() => _ffprobePath;

    public async Task<ToolExecutionResult> RunToolAsync(
        string toolName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        Action<string>? onStandardOutputLine = null,
        Action<string>? onStandardErrorLine = null,
        string? workingDirectory = null)
    {
        var executablePath = toolName.ToLowerInvariant() switch
        {
            "yt-dlp" => _ytDlpPath,
            "ffmpeg" => _ffmpegPath,
            "ffprobe" => _ffprobePath,
            _ => toolName
        };

        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(15);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(effectiveTimeout);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.Environment["PYTHONUNBUFFERED"] = "1";

        if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var sanitizedCommand = SanitizeLogArguments(executablePath, arguments);
        _logger.LogInformation("[BREAKPOINT:ToolRunner:START] Spawning {ToolName}: {Command}", toolName, sanitizedCommand);

        using var process = new Process { StartInfo = startInfo };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        bool timedOut = false;
        bool cancelledByUser = false;

        using var cancelRegistration = linkedCts.Token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelledByUser = true;
                    }
                    else
                    {
                        timedOut = true;
                    }
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { }
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            process.Start();

            var stdoutTask = Task.Run(async () =>
            {
                using var reader = process.StandardOutput;
                while (await reader.ReadLineAsync() is { } line)
                {
                    stdoutBuilder.AppendLine(line);
                    onStandardOutputLine?.Invoke(line);
                }
            });

            var stderrTask = Task.Run(async () =>
            {
                using var reader = process.StandardError;
                while (await reader.ReadLineAsync() is { } line)
                {
                    stderrBuilder.AppendLine(line);
                    onStandardErrorLine?.Invoke(line);
                }
            });

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();
            sw.Stop();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("[ToolRunner:TIMING] Tool {ToolName} completed successfully in {ElapsedMs}ms. Command: {Command}", 
                    toolName, sw.ElapsedMilliseconds, sanitizedCommand);
            }
            else
            {
                _logger.LogWarning("[ToolRunner:FAIL] Tool {ToolName} exited with code {ExitCode} after {ElapsedMs}ms. Command: {Command}. Stderr: {Err}",
                    toolName, process.ExitCode, sw.ElapsedMilliseconds, sanitizedCommand, stderrBuilder.ToString());
            }

            return new ToolExecutionResult(
                process.ExitCode,
                stdoutBuilder.ToString(),
                stderrBuilder.ToString(),
                timedOut,
                cancelledByUser);
        }
        catch (Exception ex) when (linkedCts.IsCancellationRequested)
        {
            _logger.LogWarning("Tool execution was cancelled or timed out ({Tool}): {Message}", toolName, ex.Message);
            return new ToolExecutionResult(
                -1,
                stdoutBuilder.ToString(),
                stderrBuilder.ToString(),
                timedOut,
                cancelledByUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start or execute media tool {Tool}", toolName);
            throw;
        }
    }

    private static string ResolveExecutablePath(string? configuredPath, string fallbackName, params string[] candidatePaths)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var withExe = configuredPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? configuredPath
                    : configuredPath + ".exe";
                if (File.Exists(withExe))
                {
                    return withExe;
                }
            }
        }

        foreach (var candidate in candidatePaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // On Windows, also test if <fallbackName>.exe exists in PATH or common application directories
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var exeName = fallbackName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? fallbackName
                : fallbackName + ".exe";

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var folder in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var full = Path.Combine(folder.Trim(), exeName);
                if (File.Exists(full)) return full;
            }

            // Check well-known Python Scripts folders in LocalAppData and AppData
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                var localPythonDir = Path.Combine(localAppData, "Python");
                if (Directory.Exists(localPythonDir))
                {
                    try
                    {
                        foreach (var subDir in Directory.GetDirectories(localPythonDir))
                        {
                            var scriptExe = Path.Combine(subDir, "Scripts", exeName);
                            if (File.Exists(scriptExe)) return scriptExe;
                        }
                    }
                    catch { }
                }

                var programsPythonDir = Path.Combine(localAppData, "Programs", "Python");
                if (Directory.Exists(programsPythonDir))
                {
                    try
                    {
                        foreach (var subDir in Directory.GetDirectories(programsPythonDir))
                        {
                            var scriptExe = Path.Combine(subDir, "Scripts", exeName);
                            if (File.Exists(scriptExe)) return scriptExe;
                        }
                    }
                    catch { }
                }

                var wingetLinks = Path.Combine(localAppData, "Microsoft", "WinGet", "Links", exeName);
                if (File.Exists(wingetLinks)) return wingetLinks;

                var wingetPackages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(wingetPackages))
                {
                    try
                    {
                        var matches = Directory.GetFiles(wingetPackages, exeName, SearchOption.AllDirectories);
                        if (matches.Length > 0) return matches[0];
                    }
                    catch { }
                }
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                var scoopShim = Path.Combine(userProfile, "scoop", "shims", exeName);
                if (File.Exists(scoopShim)) return scoopShim;

                var localBin = Path.Combine(userProfile, ".local", "bin", exeName);
                if (File.Exists(localBin)) return localBin;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                var roamPythonDir = Path.Combine(appData, "Python");
                if (Directory.Exists(roamPythonDir))
                {
                    try
                    {
                        foreach (var subDir in Directory.GetDirectories(roamPythonDir))
                        {
                            var scriptExe = Path.Combine(subDir, "Scripts", exeName);
                            if (File.Exists(scriptExe)) return scriptExe;
                        }
                    }
                    catch { }
                }
            }
        }

        return fallbackName;
    }

    private static string SanitizeLogArguments(string executable, IReadOnlyList<string> arguments)
    {
        var sb = new StringBuilder(Path.GetFileName(executable));
        foreach (var arg in arguments)
        {
            sb.Append(' ');
            if (arg.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
                arg.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                arg.Contains("auth", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\"[REDACTED]\"");
            }
            else if (arg.Contains(' ') || arg.Contains('"'))
            {
                sb.Append('"').Append(arg.Replace("\"", "\\\"")).Append('"');
            }
            else
            {
                sb.Append(arg);
            }
        }
        return sb.ToString();
    }
}
