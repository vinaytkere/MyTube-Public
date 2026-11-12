using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Ai;

public class AiOrchestrator : IAiOrchestrator
{
    private readonly VideoDbContext _db;
    private readonly IMediaProcessor _mediaProcessor;
    private readonly IEnumerable<IAiAudioTranscriptionService> _transcriptionServices;
    private readonly IEnumerable<IAiCompletionService> _completionServices;
    private readonly IAiService? _aiService;
    private readonly ILogger<AiOrchestrator> _logger;

    public AiOrchestrator(
        VideoDbContext db,
        IMediaProcessor mediaProcessor,
        IAiService aiService,
        ILogger<AiOrchestrator> logger)
        : this(db, mediaProcessor, Array.Empty<IAiAudioTranscriptionService>(), Array.Empty<IAiCompletionService>(), logger, aiService)
    {
    }

    public AiOrchestrator(
        VideoDbContext db,
        IMediaProcessor mediaProcessor,
        IEnumerable<IAiAudioTranscriptionService> transcriptionServices,
        IEnumerable<IAiCompletionService> completionServices,
        ILogger<AiOrchestrator> logger,
        IAiService? aiService = null)
    {
        _db = db;
        _mediaProcessor = mediaProcessor;
        _transcriptionServices = transcriptionServices;
        _completionServices = completionServices;
        _logger = logger;
        _aiService = aiService;
    }

    public async Task<bool> AnalyzeVideoAsync(Guid videoId)
    {
        var video = await _db.Videos.Include(v => v.Category).FirstOrDefaultAsync(v => v.Id == videoId);
        if (video == null) return false;

        video.AiStatus = "Processing";
        await _db.SaveChangesAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "MyTubeAiTemp", video.Id.ToString());

        try
        {
            _logger.LogInformation($"Starting AI processing for video: {video.DisplayTitle} ({video.Id})");

            // 1. Extract lightweight audio chunks
            List<string> audioChunks;
            try
            {
                audioChunks = await _mediaProcessor.ExtractAudioChunksAsync(video.FilePath, tempDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Audio extraction failed for video {video.Id}");
                video.AiStatus = "Failed";
                video.ProcessingError = ex.Message;
                await _db.SaveChangesAsync();
                return false;
            }
            
            string fullTranscript = string.Empty;
            if (audioChunks.Count > 0)
            {
                _logger.LogInformation($"Transcribing {audioChunks.Count} audio chunk(s) concurrently for video {video.Id}...");
                using var semaphore = new SemaphoreSlim(3);
                var transcriptDict = new ConcurrentDictionary<int, string>();

                var tasks = audioChunks.Select(async (chunk, index) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string? chunkText = null;
                        if (_aiService != null)
                        {
                            chunkText = await _aiService.TranscribeAudioAsync(chunk);
                        }

                        if (string.IsNullOrWhiteSpace(chunkText))
                        {
                            foreach (var sttService in _transcriptionServices)
                            {
                                chunkText = await sttService.TranscribeAudioAsync(chunk);
                                if (!string.IsNullOrWhiteSpace(chunkText))
                                {
                                    break;
                                }
                                _logger.LogWarning($"STT provider {sttService.ProviderName} yielded no transcript for chunk {index}. Trying fallback STT provider...");
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(chunkText))
                        {
                            transcriptDict[index] = chunkText.Trim();
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                        try
                        {
                            if (File.Exists(chunk))
                            {
                                File.Delete(chunk);
                            }
                        }
                        catch { }
                    }
                });

                await Task.WhenAll(tasks);

                var transcriptParts = transcriptDict
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value)
                    .ToList();

                fullTranscript = string.Join(" ", transcriptParts);
            }

            // Purge any remaining temporary MP3 files in temp directory
            CleanTempDirectory(tempDir);

            if (string.IsNullOrWhiteSpace(fullTranscript))
            {
                fullTranscript = $"[No spoken audio transcribed. Raw filename: {video.FileName}]";
            }

            // 2. Fetch existing categories for prompt context
            var existingCategories = await _db.Categories.Select(c => c.Name).ToListAsync();
            var categoryContext = string.Join(", ", existingCategories);

            // 3. Process text analysis through unified IAiService or completion pipeline
            AiAnalysisResult? analysis = null;

            if (_aiService != null)
            {
                _logger.LogInformation($"Attempting video text analysis via unified IAiService");
                analysis = await _aiService.AnalyzeTextAsync(video.FileName, fullTranscript, categoryContext);
            }

            if (analysis == null)
            {
                var systemPrompt = AiPrompts.BuildMetadataSystemPrompt(categoryContext);
                var userPrompt = AiPrompts.BuildMetadataUserPrompt(video.FileName, fullTranscript);

                foreach (var service in _completionServices)
                {
                    _logger.LogInformation($"Attempting video text analysis via provider: {service.ProviderName}");
                    var responseJson = await service.CompleteAsync(systemPrompt, userPrompt, forceJsonMode: true);
                    if (!string.IsNullOrWhiteSpace(responseJson))
                    {
                        try
                        {
                            analysis = JsonSerializer.Deserialize<AiAnalysisResult>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (analysis != null)
                            {
                                _logger.LogInformation($"Successfully analyzed video metadata using provider: {service.ProviderName}");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to deserialize JSON response from provider {service.ProviderName}. Trying next provider...");
                        }
                    }
                }
            }

            if (analysis == null)
            {
                _logger.LogWarning($"All AI providers failed for video {video.Id}. Saving partial transcript.");
                video.Transcript = fullTranscript;
                video.AiStatus = "Failed";
                video.ProcessingError = "All AI providers (Groq/Gemini/DeepSeek) returned null or exhausted quotas. Check environment variables & quota limits on server.";
                await _db.SaveChangesAsync();
                return false;
            }

            // 4. Assign results & virtual category link
            video.Transcript = fullTranscript;
            video.Summary = analysis.Summary;
            video.TagsJson = JsonSerializer.Serialize(analysis.Tags);
            video.DetectedLanguage = analysis.DetectedLanguage;
            if (!string.IsNullOrWhiteSpace(analysis.SuggestedTitle))
            {
                video.DisplayTitle = analysis.SuggestedTitle.Trim();
            }

            // Match existing user-created or system category without auto-creating new categories
            if (!string.IsNullOrWhiteSpace(analysis.SuggestedCategory))
            {
                var categoryName = FormatCategoryName(analysis.SuggestedCategory);
                var matchingCategory = await _db.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower());
                if (matchingCategory != null)
                {
                    // Only assign if the video is currently in the default Downloads folder
                    var currentCat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == video.CategoryId);
                    if (currentCat == null || !currentCat.IsUserCreated || string.Equals(currentCat.Name, "Downloads", StringComparison.OrdinalIgnoreCase))
                    {
                        video.CategoryId = matchingCategory.Id;
                    }
                }
            }

            video.AiStatus = "Completed";
            video.AiProcessedAt = DateTime.UtcNow;
            video.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation($"AI processing completed successfully for video: {video.DisplayTitle}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"AI processing error for video {video.Id}");
            video.AiStatus = "Failed";
            video.ProcessingError = ex.Message;
            await _db.SaveChangesAsync();
            return false;
        }
        finally
        {
            // Delete all temp MP3 files and directory
            CleanTempDirectory(tempDir);
        }
    }

    public async Task<int> BatchAnalyzeVideosAsync(IEnumerable<Guid> videoIds)
    {
        int count = 0;
        foreach (var id in videoIds)
        {
            if (await AnalyzeVideoAsync(id))
            {
                count++;
            }
        }
        return count;
    }

    public async Task<string> GenerateQaAnswerAsync(string videoTitle, string question, string webContext, string videoSummary, string transcript)
    {
        _logger.LogInformation($"Generating Q&A answer for question: '{question}' on video: '{videoTitle}'");

        var systemPrompt = AiPrompts.BuildQaSystemPrompt();
        var transcriptExcerpt = string.IsNullOrWhiteSpace(transcript) ? "" : (transcript.Length > 1500 ? transcript.Substring(0, 1500) + "..." : transcript);
        var userPrompt = AiPrompts.BuildQaUserPrompt(videoTitle, videoSummary, transcriptExcerpt, question, webContext);

        foreach (var service in _completionServices)
        {
            _logger.LogInformation($"Attempting Q&A answer generation via provider: {service.ProviderName}");
            var answer = await service.CompleteAsync(systemPrompt, userPrompt);
            if (!string.IsNullOrWhiteSpace(answer))
            {
                _logger.LogInformation($"Q&A answer synthesized successfully using AI provider: {service.ProviderName}");
                return answer;
            }
        }

        // Fallback Synthesizer (Pure C#) if all AI model providers fail or have exhausted quotas
        _logger.LogWarning("All LLM providers unavailable for Q&A formatting. Using smart context extraction synthesizer.");
        return SynthesizeAnswerFromContext(question, videoTitle, webContext, videoSummary, transcript);
    }

    private void CleanTempDirectory(string tempDir)
    {
        try
        {
            if (!Directory.Exists(tempDir)) return;

            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch { }
            }

            Directory.Delete(tempDir, true);
            _logger.LogInformation($"Successfully cleaned up temporary AI audio files and directory: {tempDir}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to completely clean up temp AI directory {tempDir}: {ex.Message}");
        }
    }

    private static string FormatCategoryName(string rawName)
    {
        var name = rawName.Replace('_', ' ').Replace('-', ' ').Trim();
        if (name.Length > 0)
        {
            name = char.ToUpper(name[0]) + name[1..].ToLower();
        }
        return string.IsNullOrEmpty(name) ? "Other" : name;
    }

    private static string SynthesizeAnswerFromContext(string question, string videoTitle, string rawContext, string summary, string transcript)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Web & Video Answer Insights");
        sb.AppendLine();
        sb.AppendLine($"**Question:** {question}");
        sb.AppendLine();

        var qWords = question.ToLower().Split(new[] { ' ', '?', ',', '.', '!', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !new[] { "what", "who", "where", "when", "why", "how", "this", "that", "with", "from", "have", "been", "the", "and", "is", "are" }.Contains(w))
            .ToList();

        // 1. Process web context if present
        if (!string.IsNullOrWhiteSpace(rawContext))
        {
            var text = System.Text.RegularExpressions.Regex.Replace(rawContext, @"!\[.*?\]\(.*?\)", string.Empty);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"https://www\.youtube\.com/redirect\?[^\s\)]+", string.Empty);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\*\*([^\*]+)\*\*\s+\*\*\1\*\*[^\]]*\]", "[$1]");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");

            sb.AppendLine("#### Web Search Key Details");
            var snippets = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            int addedCount = 0;
            foreach (var snip in snippets)
            {
                var cleanSnip = snip.Trim();
                if (string.IsNullOrEmpty(cleanSnip)) continue;

                if (qWords.Any(w => cleanSnip.ToLower().Contains(w)))
                {
                    sb.AppendLine($"- {cleanSnip}");
                    addedCount++;
                }
            }

            if (addedCount == 0 && snippets.Length > 0)
            {
                foreach (var snip in snippets.Take(2))
                {
                    sb.AppendLine($"- {snip.Trim()}");
                }
            }
            sb.AppendLine();
        }

        // 2. Video Transcript / Summary Context
        sb.AppendLine("#### Video Context");
        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.AppendLine($"- **Summary:** {summary}");
        }

        if (!string.IsNullOrWhiteSpace(transcript))
        {
            var matchingSentence = transcript.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => qWords.Any(w => s.ToLower().Contains(w)));

            if (!string.IsNullOrWhiteSpace(matchingSentence))
            {
                sb.AppendLine($"- **Relevant Audio Transcript:** \"{matchingSentence}.\"");
            }
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"*Synthesized from Firecrawl web search & video metadata for **{videoTitle}**.*");

        return sb.ToString().Trim();
    }
}
