using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Features.Auth.Commands;

public record SendOtpCommand(string Email, string FullName, string Purpose = "register") : IRequest<SendOtpResult>;

public class SendOtpCommandValidator : AbstractValidator<SendOtpCommand>
{
    public SendOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Please provide a valid email address.");
    }
}

public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, SendOtpResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public SendOtpCommandHandler(IUserRepository userRepository, IEmailService emailService)
    {
        _userRepository = userRepository;
        _emailService = emailService;
    }

    public async Task<SendOtpResult> Handle(SendOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser != null && request.Purpose == "register")
        {
            throw new ValidationException("An account with this email already exists.");
        }

        var otpCode = Random.Shared.Next(100000, 999999).ToString();

        await _userRepository.InvalidateOtpsAsync(normalizedEmail, cancellationToken);

        var otpRecord = new OtpRecord
        {
            Email = normalizedEmail,
            Code = otpCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.SaveOtpAsync(otpRecord, cancellationToken);

        var recipientName = !string.IsNullOrWhiteSpace(request.FullName)
            ? request.FullName.Trim()
            : normalizedEmail.Split('@')[0];

        var (emailSent, emailErr) = await _emailService.SendOtpEmailAsync(normalizedEmail, recipientName, otpCode);
        if (!emailSent)
        {
            throw new InvalidOperationException($"Brevo Email error: {emailErr}");
        }

        return new SendOtpResult
        {
            Message = "6-digit verification code sent to your email address.",
            Email = normalizedEmail
        };
    }
}
