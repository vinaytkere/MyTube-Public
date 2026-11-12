using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.AI;

public interface ITranscriptionProvider
{
    string ProviderName { get; }
    Task<TranscriptionProviderResult?> TranscribeAudioStreamAsync(Stream audioStream, string mimeType, CancellationToken cancellationToken = default);
}

public class TranscriptionProviderResult
{
    public string Text { get; set; } = string.Empty;
    public string DetectedLanguage { get; set; } = "en";
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}
