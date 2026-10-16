using home_test_agit.Auth;
using home_test_agit.Models;
using home_test_agit.Repository;
using home_test_agit.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace home_test_agit.Controllers;

[Authorize]
public class RequestsController : Controller
{
    private readonly IAccessRequestService _service;
    private readonly IMasterDataRepository _masterData;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(
        IAccessRequestService service,
        IMasterDataRepository masterData,
        IUserRepository users,
        ICurrentUser currentUser,
        ILogger<RequestsController> logger)
    {
        _service = service;
        _masterData = masterData;
        _users = users;
        _currentUser = currentUser;
        _logger = logger;
    }

    private int ActorId => _currentUser.UserId!.Value;

    // ------------------------------------------------------------- MY REQUESTS

    /// <summary>My Requests: hanya milik user yang login (AC-19).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetMyRequestsAsync(ActorId, ct));

    /// <summary>Semua request untuk Auditor (AC-18); service melempar 403 bagi yang lain.</summary>
    [HttpGet]
    public async Task<IActionResult> All(CancellationToken ct)
        => View(await _service.GetAllAsync(ActorId, ct));

    // ------------------------------------------------------------------ DETAIL

    [HttpGet]
    public Task<IActionResult> Details(Guid id, string? conflictCode, string? conflictMessage, CancellationToken ct)
        => RenderDetailsAsync(id, conflictCode, conflictMessage, statusCode: null, ct);

    // ------------------------------------------------------------------ CREATE

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var actor = await _users.GetByIdAsync(ActorId, ct);
        return View(new CreateRequestViewModel
        {
            Applications = await _masterData.ListApplicationsAsync(ct),
            // D-11: tanpa manager, request tidak akan pernah punya approver tahap pertama.
            CanCreate = actor?.ManagerId is not null,
            Notice = actor?.ManagerId is null
                ? "Anda tidak memiliki atasan, sehingga tidak bisa mengajukan request akses."
                : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRequestViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await ReRenderCreateAsync(model, StatusCodes.Status400BadRequest, ct);

        var command = new CreateAccessRequestCommand(
            model.ClientRequestId,
            model.ApplicationId,
            model.Environment,
            model.AccessLevel,
            model.Justification);

        try
        {
            var result = await _service.CreateAsync(ActorId, command, ct);

            TempData["Flash"] = result.IsReplay
                ? "Request ini sudah pernah dikirim. Yang ditampilkan adalah request yang sama (idempotent)."
                : "Request akses berhasil dibuat dan menunggu persetujuan atasan.";

            return RedirectToAction(nameof(Details), new { id = result.RequestId });
        }
        catch (ValidationFailedException ex)
        {
            // AC-11: validasi server-side dikembalikan per-field dengan status 400.
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages)
                    ModelState.AddModelError(field, message);

            return await ReRenderCreateAsync(model, StatusCodes.Status400BadRequest, ct);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning("Create conflict {Code} for user {ActorId}", ex.Code, ActorId);
            return await ReRenderCreateAsync(model, StatusCodes.Status409Conflict, ct, ex.Code, ex.Message);
        }
    }

    private async Task<IActionResult> ReRenderCreateAsync(
        CreateRequestViewModel model, int statusCode, CancellationToken ct,
        string? conflictCode = null, string? conflictMessage = null)
    {
        var actor = await _users.GetByIdAsync(ActorId, ct);
        model.Applications = await _masterData.ListApplicationsAsync(ct);
        model.CanCreate = actor?.ManagerId is not null;
        Response.StatusCode = statusCode;
        ViewData["ConflictCode"] = conflictCode;
        ViewData["ConflictMessage"] = conflictMessage;
        return View(model);
    }

    // --------------------------------------------------------- APPROVE / REJECT

    /// <summary>Approve tahap aktif. Version ikut dikirim agar aksi basi tertolak 409 (AC-06).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, int version, CancellationToken ct)
    {
        try
        {
            await _service.ApproveAsync(ActorId, id, version, ct);
            TempData["Flash"] = "Request disetujui.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ValidationFailedException ex)
        {
            return await RenderDetailsAsync(id, "validation_failed", FirstMessage(ex),
                StatusCodes.Status400BadRequest, ct);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning("Approve conflict {Code} on request {RequestId}", ex.Code, id);
            return await RenderDetailsAsync(id, ex.Code, ex.Message, StatusCodes.Status409Conflict, ct);
        }
    }

    /// <summary>Reject tahap aktif. Reason wajib (AC-08).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, int version, string? reason, CancellationToken ct)
    {
        try
        {
            await _service.RejectAsync(ActorId, id, version, reason, ct);
            TempData["Flash"] = "Request ditolak.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ValidationFailedException ex)
        {
            // 400: reason wajib saat reject, state tidak berubah.
            return await RenderDetailsAsync(id, "validation_failed", FirstMessage(ex),
                StatusCodes.Status400BadRequest, ct);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning("Reject conflict {Code} on request {RequestId}", ex.Code, id);
            return await RenderDetailsAsync(id, ex.Code, ex.Message, StatusCodes.Status409Conflict, ct);
        }
    }

    private static string FirstMessage(ValidationFailedException ex)
        => ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Input tidak valid.";

    /// <summary>
    /// Menampilkan detail beserta banner konflik (SPECS 3.8). Status code eksplisit
    /// supaya 400/409 terlihat oleh klien, bukan 200.
    /// </summary>
    private async Task<IActionResult> RenderDetailsAsync(
        Guid id, string? conflictCode, string? conflictMessage, int? statusCode, CancellationToken ct)
    {
        var request = await _service.GetDetailAsync(ActorId, id, ct);
        var actor = await _users.GetByIdAsync(ActorId, ct);

        if (statusCode is not null)
            Response.StatusCode = statusCode.Value;

        return View("Details", new RequestDetailViewModel
        {
            Request = request,
            CanAct = actor is not null && ApprovalAuthorization.CanAct(actor, request),
            ConflictCode = conflictCode,
            ConflictMessage = conflictMessage
        });
    }
}
