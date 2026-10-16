using System.Security.Claims;
using System.Text.Encodings.Web;
using home_test_agit.Models;
using home_test_agit.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace home_test_agit.Auth;

/// <summary>
/// Auth simulasi (D-16). Sumber identitas:
///   1) header <c>X-Demo-User</c> — hanya jika Auth:AllowHeader = true (dipakai test);
///   2) cookie <c>arh.demo</c>   — diisi oleh AccountController.Switch.
/// Tidak ada sandi, tidak ada SSO: sesuai batas scope Phase 1.
/// </summary>
public class DemoAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Demo";
    public const string CookieName = "arh.demo";

    private readonly IUserRepository _users;
    private readonly IConfiguration _configuration;

    public DemoAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserRepository users,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _users = users;
        _configuration = configuration;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Header hanya aktif bila dikonfigurasi eksplisit; default false (D-16).
        if (_configuration.GetValue<bool>("Auth:AllowHeader")
            && Request.Headers.TryGetValue("X-Demo-User", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            var byEmail = await _users.GetByEmailAsync(header.ToString().Trim());
            if (byEmail is not null)
                return AuthenticateResult.Success(Ticket(byEmail));
        }

        // 2. Cookie dari user switcher.
        if (Request.Cookies.TryGetValue(CookieName, out var raw)
            && int.TryParse(raw, out var id))
        {
            var user = await _users.GetByIdAsync(id);
            if (user is not null)
                return AuthenticateResult.Success(Ticket(user));
        }

        return AuthenticateResult.NoResult();
    }

    /// <summary>Belum login diarahkan ke user switcher; bukan 401 polos.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Request.Path.StartsWithSegments("/Account"))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        var returnUrl = Uri.EscapeDataString(Request.Path + Request.QueryString);
        Response.Redirect($"/Account/Switch?returnUrl={returnUrl}");
        return Task.CompletedTask;
    }

    private static AuthenticationTicket Ticket(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email)
        };
        if (user.IsAuditor) 
            claims.Add(new Claim("is_auditor", "true"));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
    }
}
