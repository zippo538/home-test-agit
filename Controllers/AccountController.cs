using home_test_agit.Auth;
using home_test_agit.Models;
using home_test_agit.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace home_test_agit.Controllers;

/// <summary>User switcher: login simulasi, tanpa sandi (D-16).</summary>
public class AccountController : Controller
{
    private readonly IUserRepository _users;

    public AccountController(IUserRepository users) => _users = users;

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Switch(string? returnUrl, CancellationToken ct)
    {
        var model = new SwitchUserViewModel
        {
            Users = await _users.ListAsync(ct),
            ReturnUrl = returnUrl
        };
        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch(int userId, string? returnUrl, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "User tidak dikenal.");
            return View(new SwitchUserViewModel
            {
                Users = await _users.ListAsync(ct),
                ReturnUrl = returnUrl
            });
        }

        Response.Cookies.Append(
            DemoAuthenticationHandler.CookieName,
            user.Id.ToString(),
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });

        // Local redirect saja, supaya returnUrl tidak bisa dipakai untuk open redirect.
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Requests");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutUser()
    {
        Response.Cookies.Delete(DemoAuthenticationHandler.CookieName);
        await HttpContext.SignOutAsync(DemoAuthenticationHandler.SchemeName);
        return RedirectToAction(nameof(Switch));
    }
}
