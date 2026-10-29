using AccessRequestHub.Models.Domain;
using AccessRequestHub.Models;
using AccessRequestHub.Models.Enums;
using AccessRequestHub.Persistence.Repository;
using Microsoft.Extensions.Logging;

namespace AccessRequestHub.Web.Services;

public class AccessRequestService : IAccessRequestService
{
    private const int MaxJustificationLength = 2000;

    private readonly IAccessRequestRepository _requests;
    private readonly IUserRepository _users;
    private readonly IMasterDataRepository _masterData;
    private readonly IClock _clock;
    private readonly ILogger<AccessRequestService> _logger;

    public AccessRequestService(
        IAccessRequestRepository requests,
        IUserRepository users,
        IMasterDataRepository masterData,
        IClock clock,
        ILogger<AccessRequestService> logger)
    {
        _requests = requests;
        _users = users;
        _masterData = masterData;
        _clock = clock;
        _logger = logger;
    }

    // ------------------------------------------------------------------ CREATE

    public async Task<CreateAccessRequestResult> CreateAsync(
        int actorId, CreateAccessRequestCommand command, CancellationToken ct = default)
    {
        var actor = await GetActorAsync(actorId, ct);

        // D-11: hanya user yang punya manager yang boleh membuat request.
        if (actor.ManagerId is null)
            throw new ForbiddenException("You have no manager assigned and cannot create access requests.");

        var errors = new Dictionary<string, string[]>();
        if (command.ClientRequestId == Guid.Empty)
            errors[nameof(command.ClientRequestId)] = ["ClientRequestId is required."];
        if (!Enum.IsDefined(command.Environment))
            errors[nameof(command.Environment)] = ["Environment must be NonProduction or Production."];
        if (!Enum.IsDefined(command.AccessLevel))
            errors[nameof(command.AccessLevel)] = ["AccessLevel must be Read or Admin."];

        var justification = command.Justification?.Trim();
        if (string.IsNullOrEmpty(justification))
            errors[nameof(command.Justification)] = ["Justification is required."];
        else if (justification.Length > MaxJustificationLength)
            errors[nameof(command.Justification)] = [$"Justification must be at most {MaxJustificationLength} characters."];

        var application = await _masterData.GetApplicationAsync(command.ApplicationId, ct);
        if (application is null)
            errors[nameof(command.ApplicationId)] = ["Application is not valid."];
        else if (application.SystemOwnerId == actor.Id)   // D-12
            errors[nameof(command.ApplicationId)] = ["You cannot request access to an application you own."];

        if (errors.Count > 0)
            throw new ValidationFailedException(errors);

        // Idempotency: jalur cepat untuk retry biasa.
        var existing = await _requests.GetByClientRequestIdAsync(actor.Id, command.ClientRequestId, ct);
        if (existing is not null)
            return Replay(existing, command, justification!);

        var policy = await _masterData.GetActivePolicyAsync(ct)
                     ?? throw new InvalidOperationException("No active policy version is configured.");

        var now = _clock.UtcNow;
        var request = AccessRequest.Create(
            command.ClientRequestId, actor, application!, policy,
            command.Environment, command.AccessLevel, justification!, now);
        var audit = AuditEvent.ForCreated(request, policy.Code, now);

        if (await _requests.TryAddAsync(request, audit, ct))
        {
            _logger.LogInformation(
                "AccessRequest {RequestId} created by user {ActorId} (app {ApplicationId}, {Environment}, {AccessLevel}, highRisk={HighRisk})",
                request.Id, actor.Id, application!.Id, command.Environment, command.AccessLevel, request.IsHighRisk);
            return new CreateAccessRequestResult(request.Id, IsReplay: false);
        }

        // Race: request lain dengan ClientRequestId sama menang. Ambil pemenangnya.
        var winner = await _requests.GetByClientRequestIdAsync(actor.Id, command.ClientRequestId, ct)
                     ?? throw new InvalidOperationException("Unique violation reported but no existing request found.");
        _logger.LogInformation("Concurrent duplicate create for ClientRequestId {ClientRequestId}; returning {RequestId}",
            command.ClientRequestId, winner.Id);
        return Replay(winner, command, justification!);
    }

    private static CreateAccessRequestResult Replay(AccessRequest existing, CreateAccessRequestCommand cmd, string justification)
    {
        var samePayload = existing.ApplicationId == cmd.ApplicationId
                          && existing.Environment == cmd.Environment
                          && existing.AccessLevel == cmd.AccessLevel
                          && existing.Justification == justification;

        if (!samePayload)
            throw new ConflictException("idempotency_mismatch",
                "This ClientRequestId was already used with a different payload.");

        return new CreateAccessRequestResult(existing.Id, IsReplay: true);
    }

    // ------------------------------------------------------- APPROVE / REJECT

    public Task ApproveAsync(int actorId, Guid requestId, int expectedVersion, CancellationToken ct = default)
        => DecideAsync(actorId, requestId, expectedVersion, approve: true, reason: null, ct);

    public Task RejectAsync(int actorId, Guid requestId, int expectedVersion, string? reason, CancellationToken ct = default)
        => DecideAsync(actorId, requestId, expectedVersion, approve: false, reason, ct);

    /// <summary>Urutan pengecekan mengikuti SPECS 3.4 (D-13): 404, 403, 400, 409.</summary>
    private async Task DecideAsync(
        int actorId, Guid requestId, int expectedVersion, bool approve, string? reason, CancellationToken ct)
    {
        // 1. 404
        var request = await _requests.GetByIdAsync(requestId, ct)
                      ?? throw new NotFoundException("Access request not found.");

        var actor = await GetActorAsync(actorId, ct);

        // 2. 403: self-approval
        if (request.RequesterId == actor.Id)
            throw Denied(actor, request, "self-approval");

        // 3. 403: bukan manager requester dan bukan System Owner aplikasi
        var isManager = request.Requester.ManagerId == actor.Id;
        var isSystemOwner = request.Application.SystemOwnerId == actor.Id;
        if (!isManager && !isSystemOwner)
            throw Denied(actor, request, "not an assigned approver");

        // 4. 400: reject wajib reason
        var trimmedReason = reason?.Trim();
        if (!approve && string.IsNullOrEmpty(trimmedReason))
            throw new ValidationFailedException(nameof(reason), "Reason is required when rejecting.");

        // 5. 409: terminal
        if (request.Status.IsTerminal())
            throw new ConflictException("already_final",
                $"Request is already {request.Status} and cannot be processed again.");

        // 6. 409: belum giliran peran actor
        var expected = WorkflowRules.ExpectedApprover(request.Status);
        var actorMatchesStage = expected switch
        {
            ApproverRole.Manager => isManager,
            ApproverRole.SystemOwner => isSystemOwner,
            _ => false
        };
        if (!actorMatchesStage)
            throw new ConflictException("not_your_stage",
                $"This request is waiting for {expected}, not for you.");

        // 7. 409: versi stale (cek awal; kepastian ada di conditional update)
        if (request.Version != expectedVersion)
            throw StaleVersion();

        var transition = WorkflowRules.Resolve(request.Status, request.IsHighRisk, approve);
        var now = _clock.UtcNow;
        var audit = AuditEvent.ForTransition(
            request.Id, actor.Id, transition.Action,
            request.Status, transition.To,
            newVersion: expectedVersion + 1,
            request.PolicyVersion.Code,
            approve ? null : trimmedReason,
            now);

        var ok = await _requests.TryTransitionAsync(
            request.Id, expectedVersion, request.Status, transition.To,
            approve ? null : trimmedReason, now, audit, ct);

        if (!ok)
        {
            _logger.LogWarning(
                "Stale/concurrent action on AccessRequest {RequestId} by user {ActorId} (expected version {Version})",
                request.Id, actor.Id, expectedVersion);
            throw StaleVersion();
        }

        _logger.LogInformation(
            "AccessRequest {RequestId}: {Action} by user {ActorId}, {From} -> {To}, version {Version}",
            request.Id, transition.Action, actor.Id, request.Status, transition.To, expectedVersion + 1);
    }

    private ForbiddenException Denied(User actor, AccessRequest request, string reason)
    {
        _logger.LogWarning("Forbidden: user {ActorId} on AccessRequest {RequestId} ({Reason})",
            actor.Id, request.Id, reason);
        return new ForbiddenException("You are not allowed to process this request.");
    }

    private static ConflictException StaleVersion()
        => new("stale_version", "The request was changed by someone else. Reload and try again.");

    // ---------------------------------------------------------------- QUERIES

    public async Task<List<AccessRequest>> GetMyRequestsAsync(int actorId, CancellationToken ct = default)
    {
        var actor = await GetActorAsync(actorId, ct);
        return await _requests.ListByRequesterAsync(actor.Id, ct);
    }

    public async Task<List<AccessRequest>> GetInboxAsync(int actorId, CancellationToken ct = default)
    {
        var actor = await GetActorAsync(actorId, ct);

        // Dijalankan berurutan: DbContext tidak thread-safe.
        var asManager = await _requests.ListPendingManagerAsync(actor.Id, ct);
        var asSystemOwner = await _requests.ListPendingSystemOwnerAsync(actor.Id, ct);

        return [.. asManager.Concat(asSystemOwner).OrderBy(r => r.CreatedAt)];
    }

    public async Task<List<AccessRequest>> GetAllAsync(int actorId, CancellationToken ct = default)
    {
        var actor = await GetActorAsync(actorId, ct);
        if (!actor.IsAuditor)
            throw new ForbiddenException("Only auditors can view all requests.");

        return await _requests.ListAllAsync(ct);
    }

    public async Task<AccessRequest> GetDetailAsync(int actorId, Guid requestId, CancellationToken ct = default)
    {
        var actor = await GetActorAsync(actorId, ct);
        var request = await _requests.GetDetailAsync(requestId, ct)
                      ?? throw new NotFoundException("Access request not found.");

        var canView = actor.IsAuditor
                      || request.RequesterId == actor.Id
                      || request.Requester.ManagerId == actor.Id
                      || request.Application.SystemOwnerId == actor.Id;

        // D-15: user yang tidak berhak tidak boleh tahu request itu ada.
        if (!canView)
            throw new NotFoundException("Access request not found.");

        return request;
    }

    // ---------------------------------------------------------------- HELPERS

    private async Task<User> GetActorAsync(int actorId, CancellationToken ct)
        => await _users.GetByIdAsync(actorId, ct)
           ?? throw new ForbiddenException("Unknown user.");
}
