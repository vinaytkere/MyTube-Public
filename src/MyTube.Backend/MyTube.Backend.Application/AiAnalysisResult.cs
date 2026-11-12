using System.Collections.Generic;

namespace MyTube.Backend.Application;

public class AiAnalysisResult
{
    public string SuggestedTitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string DetectedLanguage { get; set; } = string.Empty;
    public string SuggestedCategory { get; set; } = string.Empty;
}
