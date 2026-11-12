using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IAiAudioTranscriptionService
{
    string ProviderName { get; }
    Task<string?> TranscribeAudioAsync(string audioFilePath);
}
