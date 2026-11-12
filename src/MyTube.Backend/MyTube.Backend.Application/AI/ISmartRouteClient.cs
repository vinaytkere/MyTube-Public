using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.AI;

/// <summary>
/// Client interface for communicating with the SmartRoute AI Gateway deployed on OCI.
/// Routes requests through Free-tier providers (Cerebras, Groq, Gemini) with resilient in-flight failover.
/// </summary>
public interface ISmartRouteClient
{
    /// <summary>
    /// Sends media context (title, uploader, description, subtitle excerpt) to SmartRoute
    /// and receives structured media classification, canonical renaming, and summary.
    /// </summary>
    Task<MediaClassificationResult?> ClassifyAndSummarizeMediaAsync(
        string title,
        string? uploader,
        string? description,
        string? subtitleExcerpt,
        CancellationToken cancellationToken = default);

    Task<MediaClassificationResult?> ClassifyMediaWithEvidenceAsync(
        string title,
        string? uploader,
        string? description,
        string? subtitleExcerpt,
        string? mediaEvidence,
        CancellationToken cancellationToken = default) =>
        ClassifyAndSummarizeMediaAsync(title, uploader, description, subtitleExcerpt, cancellationToken);
}
