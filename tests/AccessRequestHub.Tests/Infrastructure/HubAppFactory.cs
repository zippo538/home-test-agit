using home_test_agit.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AccessRequestHub.Tests.Infrastructure;

/// <summary>
/// Menjalankan aplikasi web yang sesungguhnya dengan database SQLite file temporer (D-17).
/// File terpisah per test membuat uji concurrency memakai koneksi nyata, bukan satu
/// koneksi in-memory bersama.
/// </summary>
public class HubAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    public HubAppFactory(string dbPath) => _dbPath = dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // Arahkan ke database temporer dan aktifkan header demo khusus test (D-16).
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Default Timeout=30");
        builder.UseSetting("Auth:AllowHeader", "true");
    }

    /// <summary>Menjalankan query langsung ke database untuk verifikasi state.</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }
}
