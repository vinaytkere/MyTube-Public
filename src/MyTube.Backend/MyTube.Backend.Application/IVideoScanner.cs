using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IVideoScanner
{
    Task ScanAllAsync();
    bool IsScanning { get; }
    string ScanStatusMessage { get; }
}
