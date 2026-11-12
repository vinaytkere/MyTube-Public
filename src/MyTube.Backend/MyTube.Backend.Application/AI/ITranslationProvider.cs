using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Application.AI.Models;

namespace MyTube.Backend.Application.AI;

public interface ITranslationProvider
{
    string ProviderName { get; }
    Task<TranslationProviderResult?> TranslateTextAsync(string text, SubtitleLanguage sourceLang, SubtitleLanguage targetLang, CancellationToken cancellationToken = default);
}

public class TranslationProviderResult
{
    public string TranslatedText { get; set; } = string.Empty;
    public SubtitleLanguage TargetLanguage { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}
