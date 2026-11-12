using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyTube.Backend.Application.AI;

public interface ITranscriptionRouter
{
    Task<TranscriptionProviderResult?> TranscribeAudioStreamAsync(Stream audioStream, string mimeType, bool isLiveWorkload = true, CancellationToken cancellationToken = default);
}
