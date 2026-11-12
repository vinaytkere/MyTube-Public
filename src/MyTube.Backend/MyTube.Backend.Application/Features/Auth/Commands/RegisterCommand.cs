using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Application.Security;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Features.Auth.Commands;

public record RegisterCommand(
    string FullName,
    string Email,
    string Username,
    string Password,
    string OtpCode) : IRequest<AuthResponse>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(4).WithMessage("Password must be at least 4 characters.");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var username = !string.IsNullOrWhiteSpace(request.Username)
            ? request.Username.Trim().ToLowerInvariant()
            : normalizedEmail.Split('@')[0];

        if (await _userRepository.ExistsByUsernameOrEmailAsync(username, normalizedEmail, cancellationToken))
        {
            throw new ValidationException("User with this email or username already exists.");
        }

        var isFirstUser = !await _userRepository.AnyUsersExistAsync(cancellationToken);
        var role = isFirstUser ? "Administrator" : "User";

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            FullName = !string.IsNullOrWhiteSpace(request.FullName) ? request.FullName.Trim() : username,
            Username = username,
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Role = role,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var token = _tokenGenerator.GenerateToken(user.Username, user.Role, user.Email);

        return new AuthResponse
        {
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            Token = token
        };
    }
}
