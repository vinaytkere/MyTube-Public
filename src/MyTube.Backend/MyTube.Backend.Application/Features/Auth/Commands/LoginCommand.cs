using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Application.Security;

namespace MyTube.Backend.Application.Features.Auth.Commands;

public record LoginCommand(string Username, string Password) : IRequest<AuthResponse?>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username or email is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse?>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ISecretManagementService _secretService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ISecretManagementService secretService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _secretService = secretService;
    }

    public async Task<AuthResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Username.Trim();

        var user = await _userRepository.GetByUsernameOrEmailAsync(identifier, cancellationToken);
        if (user != null)
        {
            if (_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
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

        // Optional bootstrap admin credentials from secrets/environment (no insecure hardcoded defaults)
        var defaultAdminUser = _secretService.GetSecret("Auth:DefaultAdmin:Username");
        var defaultAdminPass = _secretService.GetSecret("Auth:DefaultAdmin:Password");
        var defaultAdminEmail = _secretService.GetSecret("Auth:DefaultAdmin:Email");

        if (!string.IsNullOrWhiteSpace(defaultAdminUser) && !string.IsNullOrWhiteSpace(defaultAdminPass))
        {
            var normId = identifier.ToLowerInvariant();
            if ((normId == defaultAdminUser.ToLowerInvariant() || (!string.IsNullOrWhiteSpace(defaultAdminEmail) && normId == defaultAdminEmail.ToLowerInvariant())) &&
                request.Password == defaultAdminPass)
            {
                var adminEmail = defaultAdminEmail ?? $"{defaultAdminUser}@localhost";
                var token = _tokenGenerator.GenerateToken(defaultAdminUser, "Administrator", adminEmail);

                return new AuthResponse
                {
                    Username = defaultAdminUser,
                    FullName = "Administrator",
                    Email = adminEmail,
                    Role = "Administrator",
                    Token = token
                };
            }
        }

        return null;
    }
}
