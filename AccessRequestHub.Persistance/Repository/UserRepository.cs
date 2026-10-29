using home_test_agit.Models;
using Microsoft.EntityFrameworkCore;

namespace home_test_agit.Repository;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<List<User>> ListAsync(CancellationToken ct = default)
        => _db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync(ct);
}
