using AccessRequestHub.Models.Enums;

namespace AccessRequestHub.Models;

/// <summary>
/// Append-only. UPDATE/DELETE ditolak trigger database (D-08).
/// Ditulis dalam transaksi yang sama dengan perubahan state (D-09).
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }

    public Guid RequestId { get; set; }
    public AccessRequest Request { get; set; } = null!;

    public int ActorId { get; set; }
    public User Actor { get; set; } = null!;

    public AuditAction Action { get; set; }

    /// <summary>Null untuk event Created.</summary>
    public RequestStatus? FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }

    /// <summary>Version request setelah transisi ini.</summary>
    public int RequestVersion { get; set; }

    /// <summary>Kode policy saat event terjadi (snapshot, contoh "v1").</summary>
    public string PolicyVersionCode { get; set; } = string.Empty;

    /// <summary>Terisi untuk Rejected.</summary>
    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public static AuditEvent ForCreated(AccessRequest request, string policyCode, DateTime utcNow) => new()
    {
        RequestId = request.Id,
        ActorId = request.RequesterId,
        Action = AuditAction.Created,
        FromStatus = null,
        ToStatus = request.Status,
        RequestVersion = request.Version,
        PolicyVersionCode = policyCode,
        OccurredAt = utcNow
    };

    public static AuditEvent ForTransition(
        Guid requestId,
        int actorId,
        AuditAction action,
        RequestStatus from,
        RequestStatus to,
        int newVersion,
        string policyCode,
        string? reason,
        DateTime utcNow) => new()
    {
        RequestId = requestId,
        ActorId = actorId,
        Action = action,
        FromStatus = from,
        ToStatus = to,
        RequestVersion = newVersion,
        PolicyVersionCode = policyCode,
        Reason = reason,
        OccurredAt = utcNow
    };
}
