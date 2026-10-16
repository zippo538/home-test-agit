using home_test_agit.Models;

namespace home_test_agit.Repository;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<List<User>> ListAsync(CancellationToken ct = default);
}
