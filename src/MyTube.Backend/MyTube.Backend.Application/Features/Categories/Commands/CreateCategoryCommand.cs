using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Features.Categories.Commands;

public record CreateCategoryCommand(string Name) : IRequest<CategoryDto>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name cannot be empty.")
            .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters.");
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;

    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var trimmedName = request.Name.Trim();

        var existing = await _categoryRepository.GetByNameAsync(trimmedName, cancellationToken);
        if (existing != null)
        {
            if (!existing.IsUserCreated)
            {
                existing.IsUserCreated = true;
                await _categoryRepository.SaveChangesAsync(cancellationToken);
            }

            return new CategoryDto
            {
                Id = existing.Id,
                Name = existing.Name,
                VideoCount = existing.Videos?.Count(v => v.ScanStatus != ScanState.Missing) ?? 0,
                IsUserCreated = existing.IsUserCreated
            };
        }

        var category = new Category
        {
            Name = trimmedName,
            CreatedAt = DateTime.UtcNow,
            IsUserCreated = true
        };

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            VideoCount = 0,
            IsUserCreated = true
        };
    }
}
