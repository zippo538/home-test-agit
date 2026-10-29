namespace home_test_agit.Models.Enums;

public enum RequestStatus
{
    PendingManager,
    PendingSystemOwner,
    Approved,
    Rejected
}

public static class RequestStatusExtensions
{
    /// <summary>Approved dan Rejected adalah terminal (tidak boleh ada transisi lagi).</summary>
    public static bool IsTerminal(this RequestStatus status)
        => status is RequestStatus.Approved or RequestStatus.Rejected;
}

