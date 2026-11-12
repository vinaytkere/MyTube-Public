using System.Threading;
using System.Threading.Tasks;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Application.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameOrEmailAsync(string identifier, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default);
    Task<bool> AnyUsersExistAsync(CancellationToken ct = default);
    Task<OtpRecord?> GetLatestValidOtpAsync(string email, string code, CancellationToken ct = default);
    Task InvalidateOtpsAsync(string email, CancellationToken ct = default);
    Task SaveOtpAsync(OtpRecord record, CancellationToken ct = default);
}
