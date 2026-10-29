using AccessRequestHub.Persistence.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AccessRequestHub.Persistence.Data;

/// <summary>
/// Membuat schema dan memasang proteksi append-only pada audit.
/// Reproducible: hapus file .db, jalankan aplikasi, skema + seed kembali (AC-21).
/// </summary>
public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseBootstrapper));

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            // Jaring pengaman bila migration belum dibuat di mesin ini.
            await db.Database.EnsureCreatedAsync(ct);
            logger.LogWarning("Tidak ada migration; schema dibuat dengan EnsureCreated.");
        }

        await AuditAppendOnlyGuard.InstallAsync(db, ct);
        logger.LogInformation("Database siap.");
    }
}

/// <summary>
/// Trigger database: UPDATE/DELETE pada AuditEvents ditolak (D-08, AC-16).
/// Invariant tetap aman walaupun ada bug di lapisan aplikasi.
/// </summary>
public static class AuditAppendOnlyGuard
{
    public static async Task InstallAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS trg_audit_no_update
            BEFORE UPDATE ON AuditEvents
            BEGIN
                SELECT RAISE(ABORT, 'AuditEvents is append-only');
            END;
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IF NOT EXISTS trg_audit_no_delete
            BEFORE DELETE ON AuditEvents
            BEGIN
                SELECT RAISE(ABORT, 'AuditEvents is append-only');
            END;
            """, ct);
    }
}
