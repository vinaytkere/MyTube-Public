using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IAiOrchestrator
{
    Task<bool> AnalyzeVideoAsync(Guid videoId);
    Task<int> BatchAnalyzeVideosAsync(IEnumerable<Guid> videoIds);
    Task<string> GenerateQaAnswerAsync(string videoTitle, string question, string webContext, string videoSummary, string transcript);
}
