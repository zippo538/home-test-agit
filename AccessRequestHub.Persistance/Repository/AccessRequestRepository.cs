using home_test_agit.Models;
using home_test_agit.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace home_test_agit.Repository;

public class AccessRequestRepository : IAccessRequestRepository
{
    private readonly AppDbContext _db;

    public AccessRequestRepository(AppDbContext db) => _db = db;

    public Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.AccessRequests.AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.PolicyVersion)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<AccessRequest?> GetDetailAsync(Guid id, CancellationToken ct = default)
        => _db.AccessRequests.AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.PolicyVersion)
            .Include(r => r.AuditEvents.OrderBy(a => a.OccurredAt).ThenBy(a => a.Id))
                .ThenInclude(a => a.Actor)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<AccessRequest?> GetByClientRequestIdAsync(int requesterId, Guid clientRequestId, CancellationToken ct = default)
        => _db.AccessRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequesterId == requesterId && r.ClientRequestId == clientRequestId, ct);

    public Task<List<AccessRequest>> ListByRequesterAsync(int requesterId, CancellationToken ct = default)
        => ListQuery().Where(r => r.RequesterId == requesterId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public Task<List<AccessRequest>> ListAllAsync(CancellationToken ct = default)
        => ListQuery().OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public Task<List<AccessRequest>> ListPendingManagerAsync(int managerId, CancellationToken ct = default)
        => ListQuery()
            .Where(r => r.Status == RequestStatus.PendingManager
                        && r.Requester.ManagerId == managerId
                        && r.RequesterId != managerId)
            .OrderBy(r => r.CreatedAt).ToListAsync(ct);

    public Task<List<AccessRequest>> ListPendingSystemOwnerAsync(int systemOwnerId, CancellationToken ct = default)
        => ListQuery()
            .Where(r => r.Status == RequestStatus.PendingSystemOwner
                        && r.Application.SystemOwnerId == systemOwnerId
                        && r.RequesterId != systemOwnerId)
            .OrderBy(r => r.CreatedAt).ToListAsync(ct);

    public async Task<bool> TryAddAsync(AccessRequest request, AuditEvent createdEvent, CancellationToken ct = default)
    {
        _db.AccessRequests.Add(request);
        _db.AuditEvents.Add(createdEvent);

        try
        {
            // Satu SaveChanges = satu transaksi: request dan audit tersimpan bersama atau tidak sama sekali.
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsClientRequestIdViolation(ex))
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> TryTransitionAsync(
        Guid requestId,
        int expectedVersion,
        RequestStatus fromStatus,
        RequestStatus toStatus,
        string? rejectReason,
        DateTime utcNow,
        AuditEvent audit,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Hanya satu pemanggil yang cocok dengan (Id, Version, Status); sisanya mendapat 0 baris.
        var rows = await _db.AccessRequests
            .Where(r => r.Id == requestId && r.Version == expectedVersion && r.Status == fromStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, toStatus)
                .SetProperty(r => r.Version, r => r.Version + 1)
                .SetProperty(r => r.RejectReason, rejectReason)
                .SetProperty(r => r.UpdatedAt, utcNow), ct);

        if (rows == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        _db.AuditEvents.Add(audit);
        await _db.SaveChangesAsync(ct);   // jika gagal, transaksi di-dispose tanpa commit (rollback)
        await tx.CommitAsync(ct);
        return true;
    }

    private IQueryable<AccessRequest> ListQuery()
        => _db.AccessRequests.AsNoTracking()
            .Include(r => r.Requester)
            .Include(r => r.Application)
            .Include(r => r.PolicyVersion);

    private static bool IsClientRequestIdViolation(DbUpdateException ex)
        => ex.InnerException is SqliteException
           {
               SqliteErrorCode: 19,
               SqliteExtendedErrorCode: 2067   // SQLITE_CONSTRAINT_UNIQUE
           } sqlite
           && sqlite.Message.Contains("ClientRequestId", StringComparison.Ordinal);
}
