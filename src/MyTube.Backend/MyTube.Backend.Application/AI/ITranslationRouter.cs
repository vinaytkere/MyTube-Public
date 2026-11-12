using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Application.AI.Models;

namespace MyTube.Backend.Application.AI;

public interface ITranslationRouter
{
    Task<TranslationProviderResult?> TranslateTextAsync(string text, SubtitleLanguage sourceLang, SubtitleLanguage targetLang, bool isLiveWorkload = true, CancellationToken cancellationToken = default);
}
