using home_test_agit.Models;

namespace home_test_agit.Services;

public record CreateAccessRequestCommand(
    Guid ClientRequestId,
    int ApplicationId,
    TargetEnvironment Environment,
    AccessLevel AccessLevel,
    string? Justification);

/// <param name="IsReplay">True jika ClientRequestId sudah pernah dipakai dan request lama dikembalikan.</param>
public record CreateAccessRequestResult(Guid RequestId, bool IsReplay);
