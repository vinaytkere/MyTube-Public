using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.FileNaming;

public interface IMediaNamingService
{
    FileNamingSettingsDto GetSettings();
    Task UpdateSettingsAsync(FileNamingSettingsDto settings, CancellationToken cancellationToken = default);
    
    string FormatFileName(Video video, string pattern, SpaceReplacementOption spaceReplacement, ColonReplacementOption colonReplacement);
    
    Task<FileRenamePreviewResultDto> PreviewRenamingAsync(FileRenamePreviewRequest request, CancellationToken cancellationToken = default);
    Task<FileRenameExecuteResultDto> ExecuteRenamingAsync(FileRenameExecuteRequest request, CancellationToken cancellationToken = default);

    Task<RecycleBinSummaryDto> GetRecycleBinSummaryAsync(CancellationToken cancellationToken = default);
    Task<int> EmptyRecycleBinAsync(CancellationToken cancellationToken = default);
}
