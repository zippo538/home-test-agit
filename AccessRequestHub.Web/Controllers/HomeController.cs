using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccessRequestHub.Web.Controllers;

/// <summary>
/// Target untuk UseExceptionHandler("/Home/Error") dan
/// AccessDeniedPath = "/Home/Forbidden" (REVIEW.md R-06).
/// </summary>
public class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult Error()
        => View("~/Views/Shared/Error.cshtml");

    [AllowAnonymous]
    public IActionResult Forbidden()
        => View("~/Views/Shared/Forbidden.cshtml");
}
