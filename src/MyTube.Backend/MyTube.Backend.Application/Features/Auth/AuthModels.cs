using System;

namespace MyTube.Backend.Application.Features.Auth;

public sealed record AuthResponse
{
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
}

public sealed record CurrentUserDto
{
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsEmailVerified { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record SendOtpResult
{
    public string Message { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed record VerifyOtpResult
{
    public string Message { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool Verified { get; init; }
}
