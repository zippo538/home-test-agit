using home_test_agit.Models.Enums;

namespace home_test_agit.Domain;

public readonly record struct Transition(AuditAction Action, RequestStatus To);

/// <summary>
/// Tabel transisi workflow (SPECS 3.3). Murni, tanpa dependency, mudah di-unit-test.
/// </summary>
public static class WorkflowRules
{
    /// <summary>Peran yang sedang ditunggu pada status tertentu. Null jika status terminal.</summary>
    public static ApproverRole? ExpectedApprover(RequestStatus status) => status switch
    {
        RequestStatus.PendingManager => ApproverRole.Manager,
        RequestStatus.PendingSystemOwner => ApproverRole.SystemOwner,
        _ => null
    };

    public static Transition Resolve(RequestStatus from, bool isHighRisk, bool approve)
    {
        if (from.IsTerminal())
            throw new InvalidOperationException($"Status {from} is terminal.");

        if (!approve)
            return new Transition(AuditAction.Rejected, RequestStatus.Rejected);

        return from switch
        {
            RequestStatus.PendingManager when isHighRisk
                => new Transition(AuditAction.ManagerApproved, RequestStatus.PendingSystemOwner),
            RequestStatus.PendingManager
                => new Transition(AuditAction.ManagerApproved, RequestStatus.Approved),
            RequestStatus.PendingSystemOwner
                => new Transition(AuditAction.SystemOwnerApproved, RequestStatus.Approved),
            _ => throw new InvalidOperationException($"Unsupported status {from}.")
        };
    }
}
