using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Repositories;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetAllWithCountAsync(CancellationToken ct = default);
}
