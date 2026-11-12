using System;
using System.Security.Cryptography;
using System.Text;

namespace MyTube.Backend.Application.Security;

public class PasswordHasher : IPasswordHasher
{
    private const string Salt = "MyTubeSalt123!";

    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + Salt));
        return Convert.ToBase64String(bytes);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return HashPassword(password) == passwordHash;
    }
}
