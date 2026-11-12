using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Features.Auth.Commands;
using MyTube.Backend.Application.Features.Auth.Queries;
using MyTube.Backend.Application.Security;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Repositories;
using Xunit;

namespace MyTube.Backend.Tests;

public class CqrsAuthTests
{
    private class FakeSecretService : ISecretManagementService
    {
        public string? GetSecret(string key, string? defaultValue = null)
        {
            return key switch
            {
                "Auth:DefaultAdmin:Username" => "admin",
                "Auth:DefaultAdmin:Password" => "Admin1234!",
                "Auth:DefaultAdmin:Email" => "admin@mytube.local",
                "Authentication:Jwt:Key" => "TestSecretKey12345678901234567890!",
                _ => defaultValue
            };
        }

        public Task<string?> GetSecretAsync(string key, string? defaultValue = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetSecret(key, defaultValue));
        }

        public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretKeys, CancellationToken cancellationToken = default)
        {
            var dict = new Dictionary<string, string>();
            foreach (var key in secretKeys)
            {
                var val = GetSecret(key);
                if (val != null) dict[key] = val;
            }
            return Task.FromResult<IReadOnlyDictionary<string, string>>(dict);
        }

        public void InvalidateCache(string? key = null) { }
    }

    private class FakeEmailService : IEmailService
    {
        public bool ShouldSucceed { get; set; } = true;
        public string LastSentCode { get; private set; } = string.Empty;

        public Task<(bool Success, string ErrorMessage)> SendOtpEmailAsync(string toEmail, string recipientName, string otpCode)
        {
            LastSentCode = otpCode;
            return Task.FromResult(ShouldSucceed ? (true, string.Empty) : (false, "SMTP Error"));
        }
    }

    private class FakeJwtGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(string username, string role, string email) => $"jwt-for-{username}";
    }

    private VideoDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VideoDbContext(options);
    }

    [Fact]
    public void LoginCommandValidator_ShouldFail_WhenFieldsEmpty()
    {
        var validator = new LoginCommandValidator();

        var result1 = validator.Validate(new LoginCommand("", "pass"));
        var result2 = validator.Validate(new LoginCommand("user", ""));

        result1.IsValid.Should().BeFalse();
        result2.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task LoginCommandHandler_ShouldAuthenticate_ExistingUser()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);
        var hasher = new PasswordHasher();
        var jwt = new FakeJwtGenerator();
        var secretService = new FakeSecretService();

        var user = new User
        {
            Username = "johndoe",
            Email = "john@example.com",
            PasswordHash = hasher.HashPassword("secretpass"),
            FullName = "John Doe",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddAsync(user);
        await repo.SaveChangesAsync();

        var handler = new LoginCommandHandler(repo, hasher, jwt, secretService);

        var response = await handler.Handle(new LoginCommand("johndoe", "secretpass"), CancellationToken.None);

        response.Should().NotBeNull();
        response!.Username.Should().Be("johndoe");
        response.Token.Should().Be("jwt-for-johndoe");
    }

    [Fact]
    public async Task LoginCommandHandler_ShouldAuthenticate_DefaultAdminFallback()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);
        var hasher = new PasswordHasher();
        var jwt = new FakeJwtGenerator();
        var secretService = new FakeSecretService();

        var handler = new LoginCommandHandler(repo, hasher, jwt, secretService);

        var response = await handler.Handle(new LoginCommand("admin", "Admin1234!"), CancellationToken.None);

        response.Should().NotBeNull();
        response!.Username.Should().Be("admin");
        response.Role.Should().Be("Administrator");
        response.Token.Should().Be("jwt-for-admin");
    }

    [Fact]
    public async Task LoginCommandHandler_ShouldReturnNull_OnInvalidPassword()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);
        var hasher = new PasswordHasher();
        var jwt = new FakeJwtGenerator();
        var secretService = new FakeSecretService();

        var handler = new LoginCommandHandler(repo, hasher, jwt, secretService);

        var response = await handler.Handle(new LoginCommand("admin", "wrongpass"), CancellationToken.None);

        response.Should().BeNull();
    }

    [Fact]
    public void RegisterCommandValidator_ShouldFail_OnInvalidEmailOrShortPassword()
    {
        var validator = new RegisterCommandValidator();

        var resEmail = validator.Validate(new RegisterCommand("Name", "not-an-email", "user", "12345", ""));
        var resPass = validator.Validate(new RegisterCommand("Name", "user@test.com", "user", "12", ""));

        resEmail.IsValid.Should().BeFalse();
        resPass.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterCommandHandler_ShouldCreateFirstUser_AsAdministrator()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);
        var hasher = new PasswordHasher();
        var jwt = new FakeJwtGenerator();

        var handler = new RegisterCommandHandler(repo, hasher, jwt);

        var response = await handler.Handle(
            new RegisterCommand("First User", "first@example.com", "firstuser", "securepass", "123456"),
            CancellationToken.None);

        response.Role.Should().Be("Administrator");
        response.Username.Should().Be("firstuser");

        var created = await repo.GetByUsernameAsync("firstuser");
        created.Should().NotBeNull();
        hasher.VerifyPassword("securepass", created!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterCommandHandler_ShouldThrowValidationException_WhenUserExists()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);
        var hasher = new PasswordHasher();
        var jwt = new FakeJwtGenerator();

        await repo.AddAsync(new User
        {
            Username = "existing",
            Email = "existing@example.com",
            PasswordHash = "hash",
            FullName = "Existing",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        });
        await repo.SaveChangesAsync();

        var handler = new RegisterCommandHandler(repo, hasher, jwt);

        var act = async () => await handler.Handle(
            new RegisterCommand("New Name", "existing@example.com", "newuser", "password123", ""),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task SendOtpWorkflow_And_VerifyOtp_ShouldSucceed()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);
        var emailService = new FakeEmailService();

        var sendHandler = new SendOtpCommandHandler(repo, emailService);
        var sendResult = await sendHandler.Handle(
            new SendOtpCommand("user@test.com", "User", "register"),
            CancellationToken.None);

        sendResult.Email.Should().Be("user@test.com");
        emailService.LastSentCode.Should().NotBeNullOrEmpty();

        var verifyHandler = new VerifyOtpCommandHandler(repo);
        var verifyResult = await verifyHandler.Handle(
            new VerifyOtpCommand("user@test.com", emailService.LastSentCode),
            CancellationToken.None);

        verifyResult.Verified.Should().BeTrue();

        // Second verification should fail as OTP is now used
        var actSecond = async () => await verifyHandler.Handle(
            new VerifyOtpCommand("user@test.com", emailService.LastSentCode),
            CancellationToken.None);

        await actSecond.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetMeQueryHandler_ShouldReturnUserProfile()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new UserRepository(db);
        await repo.AddAsync(new User
        {
            Username = "admin",
            Email = "admin@mytube.local",
            PasswordHash = "hash",
            FullName = "Admin User",
            Role = "Administrator",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await repo.SaveChangesAsync();

        var handler = new GetMeQueryHandler(repo);
        var profile = await handler.Handle(new GetMeQuery("admin"), CancellationToken.None);

        profile.Username.Should().Be("admin");
        profile.FullName.Should().Be("Admin User");
        profile.Role.Should().Be("Administrator");
    }
}
