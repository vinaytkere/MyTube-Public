namespace MyTube.Backend.Application.Security;

public interface IJwtTokenGenerator
{
    string GenerateToken(string username, string role, string email);
}
