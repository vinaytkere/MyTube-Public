using System.Collections.Generic;

namespace MyTube.Backend.Application.FileNaming;

public class FileNamingConfigurationDto
{
    public FileNamingSettingsDto Settings { get; set; } = new();
    public List<FileNamingPresetDto> Presets { get; set; } = new();
    public List<FileNamingTokenDto> AvailableTokens { get; set; } = new();
}

public class FileNamingPresetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class FileNamingTokenDto
{
    public string Token { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
