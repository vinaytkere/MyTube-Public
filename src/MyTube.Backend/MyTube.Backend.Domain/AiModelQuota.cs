using System;

namespace MyTube.Backend.Domain;

public class AiModelQuota
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty; // Groq, Gemini, DeepSeek
    public string ModelName { get; set; } = string.Empty;
    public int RpmLimit { get; set; } // Requests per minute
    public int RpdLimit { get; set; } // Requests per day
    public int AudioSecondsPerDayLimit { get; set; } // Audio duration daily limit (seconds), e.g., 28800 for Groq Whisper
    
    public int CurrentMinuteRequests { get; set; }
    public int CurrentDayRequests { get; set; }
    public int CurrentDayAudioSeconds { get; set; }
    
    public DateTime WindowStartMinute { get; set; } = DateTime.UnixEpoch;
    public DateTime WindowStartDay { get; set; } = DateTime.UnixEpoch;
    
    public bool IsEnabled { get; set; } = true;
    public int PriorityOrder { get; set; }
    public string TaskType { get; set; } = "Text"; // Text or STT

    public string Status { get; set; } = "Active"; // Active, RateLimited, ModelNotFound, AuthError, PermissionError, ProviderError, Disabled
    public DateTime? LastCheckedAt { get; set; }
    public string? LastErrorDetails { get; set; }
}
