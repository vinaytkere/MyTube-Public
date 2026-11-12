using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Categories.Commands;
using MyTube.Backend.Application.Features.Categories.Queries;

namespace MyTube.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public CategoriesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories(CancellationToken ct)
    {
        var categories = await _sender.Send(new GetCategoriesQuery(), ct);
        return Ok(categories);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await _sender.Send(new CreateCategoryCommand(request.Name), ct);
        return CreatedAtAction(nameof(GetCategories), new { id = category.Id }, category);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var success = await _sender.Send(new DeleteCategoryCommand(id), ct);
        if (!success)
        {
            return NotFound(new { message = "Category not found or cannot be deleted." });
        }
        return Ok(new { message = "Category deleted successfully." });
    }
}

public sealed record CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
}
