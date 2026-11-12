namespace MyTube.Backend.Domain;

public enum ScanState
{
    Discovered,
    Processing,
    Ready,
    Failed,
    Missing
}
