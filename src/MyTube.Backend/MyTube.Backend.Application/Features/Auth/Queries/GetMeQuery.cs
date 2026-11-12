using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyTube.Backend.Application.Repositories;

namespace MyTube.Backend.Application.Features.Auth.Queries;

public record GetMeQuery(string Username, string? FallbackRole = "User") : IRequest<CurrentUserDto>;

public class GetMeQueryHandler : IRequestHandler<GetMeQuery, CurrentUserDto>
{
    private readonly IUserRepository _userRepository;

    public GetMeQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<CurrentUserDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);
        if (user != null)
        {
            return new CurrentUserDto
            {
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified,
                CreatedAt = user.CreatedAt
            };
        }

        return new CurrentUserDto
        {
            Username = request.Username,
            FullName = request.Username,
            Email = $"{request.Username}@mytube.com",
            Role = request.FallbackRole ?? "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
