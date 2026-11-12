using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IAiCompletionService
{
    string ProviderName { get; }
    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, bool forceJsonMode = false);
}
