using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Application.MediaOrganizer.Dtos;

namespace MyTube.Backend.Application.MediaOrganizer;

public interface IMediaOrganizerService
{
    MediaOrganizerSettingsDto GetSettings();
    Task UpdateSettingsAsync(MediaOrganizerSettingsDto settings, CancellationToken cancellationToken = default);

    Task<MediaOrganizeResultDto> OrganizeMediaAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task<MediaRenamePreviewDto> PreviewOrganizationAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task<List<MediaRenamePreviewDto>> BatchPreviewAsync(IEnumerable<Guid>? videoIds = null, CancellationToken cancellationToken = default);
    Task<List<MediaOrganizeResultDto>> BatchOrganizeAsync(IEnumerable<Guid>? videoIds = null, CancellationToken cancellationToken = default);
    Task<CategoryConsolidationResultDto> ConsolidateCategoriesAsync(CancellationToken cancellationToken = default);
    Task<MediaAuditReportDto> GetAuditReportAsync(CancellationToken cancellationToken = default);
}
