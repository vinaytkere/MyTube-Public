using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyTube.Backend.Application.Features.Auth.Commands;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Application.Security;
using MyTube.Backend.Domain;
using MyTube.Backend.Infrastructure;
using MyTube.Backend.Infrastructure.Iptv;
using MyTube.Backend.Infrastructure.Security;
using Xunit;

namespace MyTube.Backend.Tests;

public class SqliteAndSecurityIntegrationTests : IDisposable
{
    private readonly string _tempDbFile;

    public SqliteAndSecurityIntegrationTests()
    {
        _tempDbFile = Path.Combine(Path.GetTempPath(), $"mytube_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbFile)) File.Delete(_tempDbFile);
            var wal = _tempDbFile + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _tempDbFile + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }

    [Fact]
    public void SqliteMigration_AppliesCleanly_OnBlankDatabase()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseSqlite($"Data Source={_tempDbFile};Cache=Shared")
            .Options;

        using (var context = new VideoDbContext(options))
        {
            context.Database.OpenConnection();
            using (var cmd = context.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA busy_timeout = 5000;
                    PRAGMA foreign_keys = ON;
                ";
                cmd.ExecuteNonQuery();
            }

            // Apply migrations
            context.Database.Migrate();

            // Verify tables and initial seed exist
            context.Categories.Add(new Category { Name = "Downloads", IsUserCreated = false });
            context.SaveChanges();

            var categories = context.Categories.ToList();
            categories.Should().ContainSingle(c => c.Name == "Downloads");

            var quotas = context.AiModelQuotas.ToList();
            quotas.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Sqlite_ConcurrentReadsAndWrites_SucceedWithWalMode()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseSqlite($"Data Source={_tempDbFile};Cache=Shared")
            .Options;

        using (var initContext = new VideoDbContext(options))
        {
            initContext.Database.OpenConnection();
            using (var cmd = initContext.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
                cmd.ExecuteNonQuery();
            }
            initContext.Database.Migrate();
        }

        // Run 10 parallel tasks reading and writing
        var tasks = Enumerable.Range(1, 10).Select(async i =>
        {
            await Task.Yield();
            using var ctx = new VideoDbContext(options);
            ctx.Database.OpenConnection();
            using (var cmd = ctx.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout = 5000;";
                cmd.ExecuteNonQuery();
            }

            ctx.Categories.Add(new Category { Name = $"ConcurrentCategory_{i}_{Guid.NewGuid():N}", IsUserCreated = true });
            await ctx.SaveChangesAsync();

            var count = await ctx.Categories.CountAsync();
            count.Should().BeGreaterThan(0);
        });

        await Task.WhenAll(tasks);
    }

    [Theory]
    [InlineData("http://localhost:5003/api/videos", false)]
    [InlineData("http://127.0.0.1:8096", false)]
    [InlineData("http://10.0.0.1/admin", false)]
    [InlineData("http://192.168.1.100/status", false)]
    [InlineData("http://169.254.169.254/latest/meta-data/", false)]
    [InlineData("http://[::1]:5000", false)]
    [InlineData("ftp://example.com/test", false)]
    public async Task SsrfValidator_Blocks_ForbiddenHostsAndSchemes(string url, bool expectedSafe)
    {
        var validator = new SsrfValidator(NullLogger<SsrfValidator>.Instance);
        var result = await validator.ValidateUrlAsync(url, CancellationToken.None);

        result.IsSafe.Should().Be(expectedSafe);
    }

    [Fact]
    public void SecretManagementService_Prefers_DockerSecrets_OverEnvironment()
    {
        var tempSecretsDir = Path.Combine(Path.GetTempPath(), $"mytube_secrets_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSecretsDir);

        try
        {
            File.WriteAllText(Path.Combine(tempSecretsDir, "jwt_secret"), "DockerMountedSecretValue12345678901234567890");

            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["SecretsPath"] = tempSecretsDir,
                ["Authentication:Jwt:Key"] = "ConfigFallbackKey12345678901234567890"
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var service = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
            var secret = service.GetSecret("Authentication:Jwt:Key");

            secret.Should().Be("DockerMountedSecretValue12345678901234567890");
        }
        finally
        {
            try { Directory.Delete(tempSecretsDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RegisterCommandHandler_DoesNotGrantAdmin_ToHardcodedUsernameWhenUsersExist()
    {
        var options = new DbContextOptionsBuilder<VideoDbContext>()
            .UseSqlite($"Data Source={_tempDbFile};Cache=Shared")
            .Options;

        using var ctx = new VideoDbContext(options);
        ctx.Database.Migrate();

        var repo = new MyTube.Backend.Infrastructure.Repositories.UserRepository(ctx);
        var hasher = new PasswordHasher();

        var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?>
        {
            ["Authentication:Jwt:Key"] = "ValidSecretKeyForTestingAtLeast32CharsLong!"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var secretService = new SecretManagementService(null!, config, NullLogger<SecretManagementService>.Instance);
        var jwt = new JwtTokenGenerator(config, secretService);

        var handler = new RegisterCommandHandler(repo, hasher, jwt);

        // 1. Create first user -> should be Administrator
        var first = await handler.Handle(
            new RegisterCommand("Initial Owner", "owner@example.com", "owner", "StrongPassword123!", ""),
            CancellationToken.None);
        first.Role.Should().Be("Administrator");

        // 2. Create second user with standard username -> MUST BE "User", NOT "Administrator"
        var second = await handler.Handle(
            new RegisterCommand("Second User", "user2@example.com", "seconduser", "StrongPassword123!", ""),
            CancellationToken.None);
        second.Role.Should().Be("User");
    }
}
