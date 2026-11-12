using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application.Features.FileNaming.Commands;
using MyTube.Backend.Application.Features.FileNaming.Queries;
using MyTube.Backend.Application.FileNaming;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
[Authorize(Roles = "Administrator")]
[Produces("application/json")]
public class FileNamingController : ControllerBase
{
    private readonly ISender _sender;

    public FileNamingController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(FileNamingConfigurationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FileNamingConfigurationDto>> GetSettings(CancellationToken ct)
    {
        var config = await _sender.Send(new GetFileNamingSettingsQuery(), ct);
        return Ok(config);
    }

    [HttpPost("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] FileNamingSettingsDto settings, CancellationToken ct)
    {
        await _sender.Send(new UpdateFileNamingSettingsCommand(settings), ct);
        return Ok(new { success = true, message = "File naming settings saved successfully." });
    }

    [HttpPost("preview")]
    [ProducesResponseType(typeof(FileRenamePreviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileRenamePreviewResultDto>> PreviewRenaming([FromBody] FileRenamePreviewRequest request, CancellationToken ct)
    {
        var preview = await _sender.Send(new PreviewFileRenamingQuery(request), ct);
        return Ok(preview);
    }

    [HttpPost("execute")]
    [ProducesResponseType(typeof(FileRenameExecuteResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileRenameExecuteResultDto>> ExecuteRenaming([FromBody] FileRenameExecuteRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new ExecuteFileRenamingCommand(request), ct);
        return Ok(result);
    }

    [HttpGet("recycle-bin")]
    [ProducesResponseType(typeof(RecycleBinSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecycleBinSummaryDto>> GetRecycleBin(CancellationToken ct)
    {
        var summary = await _sender.Send(new GetRecycleBinSummaryQuery(), ct);
        return Ok(summary);
    }

    [HttpPost("recycle-bin/empty")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> EmptyRecycleBin(CancellationToken ct)
    {
        var deletedCount = await _sender.Send(new EmptyRecycleBinCommand(), ct);
        return Ok(new { success = true, deletedCount, message = $"Recycle bin emptied. {deletedCount} files permanently deleted." });
    }
}
