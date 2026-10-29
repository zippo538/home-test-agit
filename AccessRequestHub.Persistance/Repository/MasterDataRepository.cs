using home_test_agit.Models;
using Microsoft.EntityFrameworkCore;

namespace home_test_agit.Repository;

public class MasterDataRepository : IMasterDataRepository
{
    private readonly AppDbContext _db;

    public MasterDataRepository(AppDbContext db) => _db = db;

    public Task<Application?> GetApplicationAsync(int id, CancellationToken ct = default)
        => _db.Applications.AsNoTracking().Include(a => a.SystemOwner)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<Application>> ListApplicationsAsync(CancellationToken ct = default)
        => _db.Applications.AsNoTracking().OrderBy(a => a.Name).ToListAsync(ct);

    public Task<PolicyVersion?> GetActivePolicyAsync(CancellationToken ct = default)
        => _db.PolicyVersions.AsNoTracking().FirstOrDefaultAsync(p => p.IsActive, ct);
}
