using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.FileNaming;

namespace MyTube.Backend.Application.Features.FileNaming.Commands;

public record EmptyRecycleBinCommand() : IRequest<int>;

public class EmptyRecycleBinCommandHandler : IRequestHandler<EmptyRecycleBinCommand, int>
{
    private readonly IMediaNamingService _namingService;

    public EmptyRecycleBinCommandHandler(IMediaNamingService namingService)
    {
        _namingService = namingService;
    }

    public async Task<int> Handle(EmptyRecycleBinCommand request, CancellationToken cancellationToken)
    {
        return await _namingService.EmptyRecycleBinAsync(cancellationToken);
    }
}
