using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.AI;

/// <summary>
/// Unified facade for all AI completions, audio transcriptions, and LLM providers.
/// Consolidates Cerebras, Groq, Gemini, DeepSeek, and OpenAI-compatible models behind a single class.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Executes a completion against a specifically designated provider (e.g. "Cerebras", "Groq", "DeepSeek", "Gemini").
    /// </summary>
    Task<string?> CompleteAsync(
        string providerName, 
        string systemPrompt, 
        string userPrompt, 
        bool forceJsonMode = false, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Automatically attempts completions using available providers in prioritized fallback order
    /// (Cerebras -> Groq -> Gemini -> DeepSeek) until one succeeds.
    /// </summary>
    Task<string?> CompleteWithFallbackAsync(
        string systemPrompt, 
        string userPrompt, 
        bool forceJsonMode = false, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes transcript and filename to extract structured video metadata (Title, Summary, Category, Tags).
    /// </summary>
    Task<AiAnalysisResult?> AnalyzeTextAsync(
        string originalFileName, 
        string transcript, 
        string systemCategoriesPrompt, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Formats a question-and-answer response for video queries incorporating web search context.
    /// </summary>
    Task<string?> FormatAnswerAsync(
        string videoTitle, 
        string question, 
        string webContext, 
        string videoSummary, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes an audio file into text using available STT providers (Groq Whisper, Gemini).
    /// </summary>
    Task<string?> TranscribeAudioAsync(
        string audioFilePath, 
        string? providerName = null, 
        CancellationToken cancellationToken = default);
}
