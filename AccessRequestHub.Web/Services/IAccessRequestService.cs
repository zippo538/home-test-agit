using AccessRequestHub.Models;

namespace AccessRequestHub.Web.Services;

public interface IAccessRequestService
{
    Task<CreateAccessRequestResult> CreateAsync(int actorId, CreateAccessRequestCommand command, CancellationToken ct = default);

    Task ApproveAsync(int actorId, Guid requestId, int expectedVersion, CancellationToken ct = default);
    Task RejectAsync(int actorId, Guid requestId, int expectedVersion, string? reason, CancellationToken ct = default);

    Task<List<AccessRequest>> GetMyRequestsAsync(int actorId, CancellationToken ct = default);
    Task<List<AccessRequest>> GetInboxAsync(int actorId, CancellationToken ct = default);

    /// <summary>Hanya Auditor. Selain itu ForbiddenException.</summary>
    Task<List<AccessRequest>> GetAllAsync(int actorId, CancellationToken ct = default);

    /// <summary>Detail + audit timeline. NotFoundException jika user tidak berhak melihat.</summary>
    Task<AccessRequest> GetDetailAsync(int actorId, Guid requestId, CancellationToken ct = default);
}
