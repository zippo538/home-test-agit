using home_test_agit.Models;
using home_test_agit.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccessRequestHub.Tests.Infrastructure;

/// <summary>
/// Basis untuk integration test: satu database temporer per test, dibuang setelah selesai,
/// sehingga urutan test tidak saling mempengaruhi.
/// </summary>
public abstract class IntegrationTest : IAsyncLifetime
{
    protected const string Alice = "alice@example.local";
    protected const string Bob = "bob@example.local";
    protected const string Carol = "carol@example.local";
    protected const string Dana = "dana@example.local";
    protected const string Erin = "erin@example.local";

    protected const int AliceId = 1;
    protected const int BobId = 2;
    protected const int CarolId = 3;
    protected const int DanaId = 4;
    protected const int ErinId = 5;

    protected const int CrmAppId = 1;
    protected const int FinanceAppId = 2;

    private string _dbPath = string.Empty;
    protected HubAppFactory Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"arh_test_{Guid.NewGuid():N}.db");
        Factory = new HubAppFactory(_dbPath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();

        // SQLite meninggalkan file -wal/-shm; semuanya dibersihkan agar tidak menumpuk.
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch (IOException) { /* diabaikan: pembersihan best-effort */ }
            }
        }
    }

    protected HubClient As(string email) => HubClient.For(Factory, email);

    protected Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
        => Factory.QueryAsync(query);

    // --------------------------------------------------------------- helpers

    /// <summary>Mengajukan request lewat UI dan mengembalikan ID-nya.</summary>
    protected async Task<Guid> CreateRequestAsync(
        HubClient requester,
        int applicationId = CrmAppId,
        TargetEnvironment environment = TargetEnvironment.NonProduction,
        AccessLevel accessLevel = AccessLevel.Read,
        string justification = "Need access for daily duties",
        Guid? clientRequestId = null)
    {
        var response = await requester.PostFormAsync("/Requests/Create", new Dictionary<string, string?>
        {
            ["ClientRequestId"] = (clientRequestId ?? Guid.NewGuid()).ToString(),
            ["ApplicationId"] = applicationId.ToString(),
            ["Environment"] = environment.ToString(),
            ["AccessLevel"] = accessLevel.ToString(),
            ["Justification"] = justification
        }, tokenPageUrl: "/Requests/Create");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return HubClient.RequestIdFrom(response);
    }

    protected static async Task<HttpResponseMessage> ApproveAsync(HubClient approver, Guid requestId, int version)
        => await approver.PostFormAsync("/Requests/Approve", new Dictionary<string, string?>
        {
            ["id"] = requestId.ToString(),
            ["version"] = version.ToString()
        }, tokenPageUrl: $"/Requests/Details/{requestId}");

    protected static async Task<HttpResponseMessage> RejectAsync(HubClient approver, Guid requestId, int version, string? reason)
        => await approver.PostFormAsync("/Requests/Reject", new Dictionary<string, string?>
        {
            ["id"] = requestId.ToString(),
            ["version"] = version.ToString(),
            ["reason"] = reason
        }, tokenPageUrl: $"/Requests/Details/{requestId}");

    // -------------------------------------------------------- state accessors

    protected Task<string> StatusOfAsync(Guid id)
        => QueryAsync(db => db.AccessRequests.Where(r => r.Id == id).Select(r => r.Status.ToString()).SingleAsync());

    protected Task<int> VersionOfAsync(Guid id)
        => QueryAsync(db => db.AccessRequests.Where(r => r.Id == id).Select(r => r.Version).SingleAsync());

    protected Task<int> RequestCountAsync()
        => QueryAsync(db => db.AccessRequests.CountAsync());

    protected Task<int> AuditCountAsync(Guid id)
        => QueryAsync(db => db.AuditEvents.CountAsync(a => a.RequestId == id));

    protected Task<List<string>> AuditActionsAsync(Guid id)
        => QueryAsync(db => db.AuditEvents.Where(a => a.RequestId == id)
            .OrderBy(a => a.Id).Select(a => a.Action.ToString()).ToListAsync());
}
