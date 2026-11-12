using System;
using System.Collections.Generic;

namespace MyTube.Backend.Application;

public class MediaProcessingException : Exception
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Tool { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string Stage { get; set; } = string.Empty;
    public Guid JobId { get; set; }
    public string Stderr { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public bool IsRetryable { get; set; }
    public IReadOnlyList<string> CommandArguments { get; set; } = Array.Empty<string>();
    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }

    public MediaProcessingException(string message) : base(message)
    {
        UserMessage = message;
    }

    public MediaProcessingException(string message, Exception innerException) : base(message, innerException)
    {
        UserMessage = message;
    }

    public MediaProcessingException(
        string message,
        string errorCode,
        string tool,
        int exitCode,
        string stage,
        Guid jobId,
        string stderr,
        string? userMessage = null,
        bool isRetryable = false,
        IReadOnlyList<string>? commandArguments = null,
        string? inputPath = null,
        string? outputPath = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Tool = tool;
        ExitCode = exitCode;
        Stage = stage;
        JobId = jobId;
        Stderr = stderr;
        UserMessage = string.IsNullOrWhiteSpace(userMessage) ? message : userMessage;
        IsRetryable = isRetryable;
        CommandArguments = commandArguments ?? Array.Empty<string>();
        InputPath = inputPath;
        OutputPath = outputPath;
    }
}
