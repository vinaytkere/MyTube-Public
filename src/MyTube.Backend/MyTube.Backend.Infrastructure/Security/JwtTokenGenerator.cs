using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyTube.Backend.Application.Security;

namespace MyTube.Backend.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly ISecretManagementService _secretService;

    public JwtTokenGenerator(IConfiguration configuration, ISecretManagementService secretService)
    {
        _configuration = configuration;
        _secretService = secretService;
    }

    public string GenerateToken(string username, string role, string email)
    {
        var secretKey = _secretService.GetSecret("Authentication:Jwt:Key");
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT Secret Key is not configured or too short (minimum 32 characters required). Configure 'Authentication:Jwt:Key' via /run/secrets/jwt_secret or JWT_SECRET.");
        }
        var issuer = _configuration["Authentication:Jwt:Issuer"] ?? "MyTube";
        var audience = _configuration["Authentication:Jwt:Audience"] ?? "MyTubeUsers";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email ?? string.Empty),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
