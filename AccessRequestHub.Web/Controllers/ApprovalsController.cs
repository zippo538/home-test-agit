using AccessRequestHub.Web.Auth;
using AccessRequestHub.Models;
using AccessRequestHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccessRequestHub.Web.Controllers;

[Authorize]
public class ApprovalsController : Controller
{
    private readonly IAccessRequestService _service;
    private readonly ICurrentUser _currentUser;

    public ApprovalsController(IAccessRequestService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Inbox sesuai peran: Manager melihat direct report, System Owner melihat aplikasinya (AC-13).
    /// Auditor tidak punya direct report dan tidak memiliki aplikasi, jadi inbox-nya kosong tanpa
    /// perlu cabang khusus di sini.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetInboxAsync(_currentUser.UserId!.Value, ct));
}
