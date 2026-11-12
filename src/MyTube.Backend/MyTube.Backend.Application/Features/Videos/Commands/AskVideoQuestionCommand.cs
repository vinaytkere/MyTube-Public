using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.AI;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Features.Videos.Commands;

public record AskVideoQuestionCommand(Guid VideoId, string UserId, string Question) : IRequest<VideoQaDto>;

public class AskVideoQuestionCommandValidator : AbstractValidator<AskVideoQuestionCommand>
{
    public AskVideoQuestionCommandValidator()
    {
        RuleFor(x => x.VideoId)
            .NotEmpty().WithMessage("Video ID cannot be empty.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question cannot be empty.")
            .MaximumLength(1000).WithMessage("Question cannot exceed 1000 characters.");
    }
}

public class AskVideoQuestionCommandHandler : IRequestHandler<AskVideoQuestionCommand, VideoQaDto>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IFirecrawlService _firecrawlService;
    private readonly IAiOrchestrator _aiOrchestrator;

    public AskVideoQuestionCommandHandler(
        IVideoRepository videoRepository,
        IFirecrawlService firecrawlService,
        IAiOrchestrator aiOrchestrator)
    {
        _videoRepository = videoRepository;
        _firecrawlService = firecrawlService;
        _aiOrchestrator = aiOrchestrator;
    }

    public async Task<VideoQaDto> Handle(AskVideoQuestionCommand request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetWithCategoryAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            throw new ValidationException("Video not found.");
        }

        var trimmedQuestion = request.Question.Trim();
        var searchQuery = $"{video.DisplayTitle} {trimmedQuestion}";
        var webContext = await _firecrawlService.SearchWebContextAsync(searchQuery);

        var formattedAnswer = await _aiOrchestrator.GenerateQaAnswerAsync(
            video.DisplayTitle,
            trimmedQuestion,
            webContext,
            video.Summary ?? string.Empty,
            video.Transcript ?? string.Empty);

        var cleanQuestion = SanitizePgText(trimmedQuestion);
        var cleanAnswer = SanitizePgText(formattedAnswer);
        var cleanContext = string.IsNullOrWhiteSpace(webContext) ? null : SanitizePgText(webContext);

        var qa = new VideoQa
        {
            VideoId = request.VideoId,
            UserId = SanitizePgText(request.UserId),
            Question = cleanQuestion,
            Answer = cleanAnswer,
            FirecrawlContext = cleanContext,
            CreatedAt = DateTime.UtcNow
        };

        await _videoRepository.AddQaAsync(qa, cancellationToken);

        return new VideoQaDto
        {
            Id = qa.Id,
            VideoId = qa.VideoId,
            UserId = qa.UserId,
            Question = qa.Question,
            Answer = qa.Answer,
            FirecrawlContext = qa.FirecrawlContext,
            CreatedAt = qa.CreatedAt
        };
    }

    private static string SanitizePgText(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("\0", string.Empty).Replace("\u0000", string.Empty);
    }
}
