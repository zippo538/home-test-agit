namespace home_test_agit.Models;

public class AccessRequest
{
    // Diperlukan EF Core.
    private AccessRequest() { }

    public Guid Id { get; private set; }

    /// <summary>ID dari client untuk idempotency. UNIQUE bersama RequesterId (D-05, D-06).</summary>
    public Guid ClientRequestId { get; private set; }

    /// <summary>Selalu diambil dari user yang login, bukan dari payload (AC-10).</summary>
    public int RequesterId { get; private set; }
    public User Requester { get; private set; } = null!;

    public int ApplicationId { get; private set; }
    public Application Application { get; private set; } = null!;

    public TargetEnvironment Environment { get; private set; }
    public AccessLevel AccessLevel { get; private set; }

    public string Justification { get; private set; } = string.Empty;

    /// <summary>
    /// Diubah lewat conditional update di service (ExecuteUpdateAsync), bukan lewat setter,
    /// supaya hanya satu transisi per Version yang berhasil (D-04).
    /// </summary>
    public RequestStatus Status { get; private set; }

    /// <summary>Snapshot saat create, bukan dihitung ulang (D-10).</summary>
    public bool IsHighRisk { get; private set; }

    /// <summary>Policy yang berlaku saat request dibuat (D-10).</summary>
    public int PolicyVersionId { get; private set; }
    public PolicyVersion PolicyVersion { get; private set; } = null!;

    /// <summary>Wajib terisi jika Status = Rejected (dijaga CHECK constraint).</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Concurrency token. Mulai 1, naik 1 di setiap transisi.</summary>
    public int Version { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<AuditEvent> AuditEvents { get; private set; } = new List<AuditEvent>();

    /// <summary>
    /// Membuat request baru. Selalu dimulai di PendingManager dengan Version = 1.
    /// Validasi bisnis (D-11, D-12, idempotency) dilakukan di service sebelum memanggil ini.
    /// </summary>
    public static AccessRequest Create(
        Guid clientRequestId,
        User requester,
        Application application,
        PolicyVersion policy,
        TargetEnvironment environment,
        AccessLevel accessLevel,
        string justification,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(policy);

        if (clientRequestId == Guid.Empty)
            throw new ArgumentException("ClientRequestId is required.", nameof(clientRequestId));
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("Justification is required.", nameof(justification));

        return new AccessRequest
        {
            Id = Guid.NewGuid(),
            ClientRequestId = clientRequestId,
            RequesterId = requester.Id,
            ApplicationId = application.Id,
            Environment = environment,
            AccessLevel = accessLevel,
            Justification = justification.Trim(),
            Status = RequestStatus.PendingManager,
            IsHighRisk = RiskPolicy.IsHighRisk(environment, accessLevel),
            PolicyVersionId = policy.Id,
            Version = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }
}
