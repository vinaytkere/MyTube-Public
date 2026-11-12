using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Prowlarr;

namespace MyTube.Backend.Application.Features.Radarr.Queries;

public record SearchProwlarrIndexersQuery(string Query, string? Category = null) 
    : IRequest<List<ProwlarrSearchResultDto>>;

public class SearchProwlarrIndexersQueryValidator : AbstractValidator<SearchProwlarrIndexersQuery>
{
    public SearchProwlarrIndexersQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query parameter is required.");
    }
}

public class SearchProwlarrIndexersQueryHandler : IRequestHandler<SearchProwlarrIndexersQuery, List<ProwlarrSearchResultDto>>
{
    private readonly IProwlarrService _prowlarrService;

    public SearchProwlarrIndexersQueryHandler(IProwlarrService prowlarrService)
    {
        _prowlarrService = prowlarrService;
    }

    public async Task<List<ProwlarrSearchResultDto>> Handle(SearchProwlarrIndexersQuery request, CancellationToken cancellationToken)
    {
        return await _prowlarrService.SearchAsync(request.Query, request.Category, cancellationToken);
    }
}
