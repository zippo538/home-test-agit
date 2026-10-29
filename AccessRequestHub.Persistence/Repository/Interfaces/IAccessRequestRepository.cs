using AccessRequestHub.Models;
using AccessRequestHub.Models.Enums;

namespace AccessRequestHub.Persistence.Repository;

public interface IAccessRequestRepository
{
    /// <summary>Request beserta Requester, Application (+SystemOwner), dan PolicyVersion. Tanpa audit.</summary>
    Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Request lengkap dengan audit timeline (urut waktu) dan actor tiap event.</summary>
    Task<AccessRequest?> GetDetailAsync(Guid id, CancellationToken ct = default);

    Task<AccessRequest?> GetByClientRequestIdAsync(int requesterId, Guid clientRequestId, CancellationToken ct = default);

    Task<List<AccessRequest>> ListByRequesterAsync(int requesterId, CancellationToken ct = default);
    Task<List<AccessRequest>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Inbox Manager: PendingManager milik direct report.</summary>
    Task<List<AccessRequest>> ListPendingManagerAsync(int managerId, CancellationToken ct = default);

    /// <summary>Inbox System Owner: PendingSystemOwner untuk aplikasi miliknya.</summary>
    Task<List<AccessRequest>> ListPendingSystemOwnerAsync(int systemOwnerId, CancellationToken ct = default);

    /// <summary>
    /// Menyimpan request + audit "Created" dalam satu SaveChanges (atomic).
    /// Return false jika UNIQUE (RequesterId, ClientRequestId) dilanggar (request ganda / race).
    /// </summary>
    Task<bool> TryAddAsync(AccessRequest request, AuditEvent createdEvent, CancellationToken ct = default);

    /// <summary>
    /// Conditional update (Id + Version + Status) dan insert audit dalam satu transaksi.
    /// Return false jika tidak ada baris yang cocok (versi stale atau sudah berubah); tidak ada yang tersimpan.
    /// </summary>
    Task<bool> TryTransitionAsync(
        Guid requestId,
        int expectedVersion,
        RequestStatus fromStatus,
        RequestStatus toStatus,
        string? rejectReason,
        DateTime utcNow,
        AuditEvent audit,
        CancellationToken ct = default);
}
