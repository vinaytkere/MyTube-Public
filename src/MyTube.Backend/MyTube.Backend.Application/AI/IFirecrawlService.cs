using System.Threading.Tasks;

namespace MyTube.Backend.Application.AI;

public interface IFirecrawlService
{
    Task<string> SearchWebContextAsync(string query);
}
