using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Application.Repositories;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure.Repositories;

public class CategoryRepository : EfRepository<Category>, ICategoryRepository
{
    public CategoryRepository(VideoDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Category?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return await DbSet.FirstOrDefaultAsync(c => c.Name.ToLower() == normalized, ct);
    }

    public async Task<IReadOnlyList<Category>> GetAllWithCountAsync(CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(c => c.Videos)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }
}
