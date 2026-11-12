using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace MyTube.Backend.Application.Features.Categories.Commands;

public record DeleteCategoryCommand(int CategoryId) : IRequest<bool>;

public class DeleteCategoryCommandValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Category ID must be greater than 0.");
    }
}

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, bool>
{
    private readonly IVideoService _videoService;

    public DeleteCategoryCommandHandler(IVideoService videoService)
    {
        _videoService = videoService;
    }

    public async Task<bool> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        return await _videoService.DeleteCategoryAsync(request.CategoryId, cancellationToken);
    }
}
