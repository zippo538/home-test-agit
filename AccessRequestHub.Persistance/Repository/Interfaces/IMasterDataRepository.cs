using home_test_agit.Models;

namespace home_test_agit.Repository;

public interface IMasterDataRepository
{
    Task<Application?> GetApplicationAsync(int id, CancellationToken ct = default);
    Task<List<Application>> ListApplicationsAsync(CancellationToken ct = default);
    Task<PolicyVersion?> GetActivePolicyAsync(CancellationToken ct = default);
}
