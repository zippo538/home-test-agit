using AccessRequestHub.Models;
using AccessRequestHub.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccessRequestHub.Web.Controllers;

public sealed class AccountController : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
        => View("Switch", new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Sso(string? returnUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && !Url.IsLocalUrl(returnUrl))
            return BadRequest("Invalid return URL.");

        return Challenge(new AuthenticationProperties
        {
            RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/Requests"
        }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
        => SignOut(
            new AuthenticationProperties { RedirectUri = Url.Action(nameof(Login), "Account") },
            "Cookies",
            OpenIdConnectDefaults.AuthenticationScheme);
}
