using AccessRequestHub.IdentityServer.Identity;
using AccessRequestHub.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AccessRequestHub.IdentityServer.Controllers;

public sealed class AccountController(
    SignInManager<IdentityUserAccount> signInManager,
    UserManager<IdentityUserAccount> userManager,
    IIdentityServerInteractionService interaction,
    MicrosoftExternalLoginLinker linker,
    IAuthenticationSchemeProvider schemes,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        var context = await interaction.GetAuthorizationContextAsync(returnUrl, HttpContext.RequestAborted);
        if (context is null)
            return BadRequest("Invalid authorization request.");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var context = await interaction.GetAuthorizationContextAsync(model.ReturnUrl, HttpContext.RequestAborted);
        if (context is null)
            return BadRequest("Invalid authorization request.");

        if (!ModelState.IsValid)
            return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Email atau kata sandi tidak valid.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Email atau kata sandi tidak valid.");
            return View(model);
        }

        return Redirect(model.ReturnUrl!);
    }

    /// <summary>Memulai challenge ke Microsoft; tombol hanya ditampilkan bila scheme terdaftar.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> External(string? returnUrl = null)
    {
        if (await schemes.GetSchemeAsync(MicrosoftExternalLoginLinker.SchemeName) is null)
            return BadRequest("Microsoft sign-in is not configured.");

        if (string.IsNullOrWhiteSpace(returnUrl)
            || await interaction.GetAuthorizationContextAsync(returnUrl, HttpContext.RequestAborted) is null)
            return BadRequest("Invalid authorization request.");

        var callbackUrl = Url.Action(nameof(ExternalCallback), new { returnUrl });
        var properties = signInManager.ConfigureExternalAuthenticationProperties(
            MicrosoftExternalLoginLinker.SchemeName, callbackUrl);
        return Challenge(properties, MicrosoftExternalLoginLinker.SchemeName);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalCallback(string? returnUrl)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return BadRequest("External sign-in failed.");

        var subject = MicrosoftExternalLoginLinker.ExtractSubject(info.Principal)
            ?? info.ProviderKey;
        var resolution = await linker.ResolveAsync(subject ?? string.Empty, HttpContext.RequestAborted);
        logger.LogInformation(
            "Microsoft callback: Outcome={Outcome}, DomainUserId={DomainUserId}, ReturnUrlValid={ReturnUrlValid}",
            resolution.Outcome,
            resolution.Account?.DomainUserId,
            !string.IsNullOrWhiteSpace(returnUrl)
            && interaction.IsValidReturnUrl(returnUrl));
        switch (resolution.Outcome)
        {
            case ExternalLoginOutcome.Success when resolution.Account is not null:
                if (string.IsNullOrWhiteSpace(returnUrl) || !interaction.IsValidReturnUrl(returnUrl))
                {
                    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                    return BadRequest("Invalid authorization request.");
                }

                await signInManager.SignInAsync(resolution.Account, isPersistent: false);
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return Redirect(returnUrl);
            case ExternalLoginOutcome.RequiresApproval:
                return View("ExternalSubject", new ExternalSubjectViewModel
                {
                    Subject = resolution.Subject ?? string.Empty,
                    MicrosoftEmail = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email),
                    ReturnUrl = returnUrl
                });
            default:
                return BadRequest("Sign-in ditolak. Akun Microsoft ini belum terdaftar atau mapping-nya tidak valid.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Logout(string? logoutId)
    {
        var context = await interaction.GetLogoutContextAsync(logoutId, HttpContext.RequestAborted);
        if (User.Identity?.IsAuthenticated == true)
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return context?.PostLogoutRedirectUri is { } redirect
            ? Redirect(redirect)
            : RedirectToAction(nameof(Login));
    }
}
