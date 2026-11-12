using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Repositories;

public class UserRepository : EfRepository<User>, IUserRepository
{
    public UserRepository(VideoDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<User?> GetByUsernameOrEmailAsync(string identifier, CancellationToken ct = default)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        return await DbSet.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized, ct);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return await DbSet.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await DbSet.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);
    }

    public async Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default)
    {
        var normUser = username.Trim().ToLowerInvariant();
        var normEmail = email.Trim().ToLowerInvariant();
        return await DbSet.AnyAsync(u => u.Username.ToLower() == normUser || u.Email.ToLower() == normEmail, ct);
    }

    public async Task<bool> AnyUsersExistAsync(CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(ct);
    }

    public async Task<OtpRecord?> GetLatestValidOtpAsync(string email, string code, CancellationToken ct = default)
    {
        var normEmail = email.Trim().ToLowerInvariant();
        var normCode = code.Trim();

        return await DbContext.OtpRecords
            .Where(o => o.Email == normEmail && o.Code == normCode && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task InvalidateOtpsAsync(string email, CancellationToken ct = default)
    {
        var normEmail = email.Trim().ToLowerInvariant();
        var previousOtps = await DbContext.OtpRecords
            .Where(o => o.Email == normEmail && !o.IsUsed)
            .ToListAsync(ct);

        foreach (var p in previousOtps)
        {
            p.IsUsed = true;
        }

        await DbContext.SaveChangesAsync(ct);
    }

    public async Task SaveOtpAsync(OtpRecord record, CancellationToken ct = default)
    {
        await DbContext.OtpRecords.AddAsync(record, ct);
        await DbContext.SaveChangesAsync(ct);
    }
}
