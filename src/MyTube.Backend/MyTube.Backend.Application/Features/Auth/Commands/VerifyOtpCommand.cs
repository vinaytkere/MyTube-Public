using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Repositories;

namespace MyTube.Backend.Application.Features.Auth.Commands;

public record VerifyOtpCommand(string Email, string Code) : IRequest<VerifyOtpResult>;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Please provide a valid email address.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("OTP code is required.");
    }
}

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, VerifyOtpResult>
{
    private readonly IUserRepository _userRepository;

    public VerifyOtpCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<VerifyOtpResult> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var code = request.Code.Trim();

        var otpRecord = await _userRepository.GetLatestValidOtpAsync(normalizedEmail, code, cancellationToken);
        if (otpRecord == null)
        {
            throw new ValidationException("Invalid OTP code.");
        }

        if (otpRecord.ExpiresAt < DateTime.UtcNow)
        {
            throw new ValidationException("OTP code has expired. Please request a new code.");
        }

        otpRecord.IsUsed = true;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new VerifyOtpResult
        {
            Message = "Email verified successfully.",
            Email = normalizedEmail,
            Verified = true
        };
    }
}
