namespace home_test_agit.Models;

// Dinamai TargetEnvironment (bukan Environment) agar tidak bentrok dengan System.Environment.
public enum TargetEnvironment
{
    NonProduction,
    Production
}

public enum AccessLevel
{
    Read,
    Admin
}

public enum RequestStatus
{
    PendingManager,
    PendingSystemOwner,
    Approved,
    Rejected
}

public enum AuditAction
{
    Created,
    ManagerApproved,
    SystemOwnerApproved,
    Rejected
}

public static class RequestStatusExtensions
{
    /// <summary>Approved dan Rejected adalah terminal (tidak boleh ada transisi lagi).</summary>
    public static bool IsTerminal(this RequestStatus status)
        => status is RequestStatus.Approved or RequestStatus.Rejected;
}
