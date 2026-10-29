using AccessRequestHub.Web.Auth;
using AccessRequestHub.Web.Filters;
using AccessRequestHub.Persistence.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AccessRequestHub.Web.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Mendaftarkan DbContext, repository, service, dan auth simulasi Access Request Hub.</summary>
    public static IServiceCollection AddAccessRequestHub(this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        // DbContext: Scoped (satu instance per HTTP request).
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        // Repository: Scoped karena bergantung pada DbContext.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMasterDataRepository, MasterDataRepository>();
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();

        // Clock stateless: Singleton.
        services.AddSingleton<IClock, SystemClock>();

        // Service: Scoped.
        services.AddScoped<IAccessRequestService, AccessRequestService>();

        // Aktor request diambil dari principal, bukan payload (AC-10).
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        var useTestAuthentication = configuration.GetValue<bool>("Auth:AllowHeader");
        if (!useTestAuthentication && !configuration.GetValue<bool>("Oidc:AllowPublicClient")
            && string.IsNullOrWhiteSpace(configuration["Oidc:ClientSecret"])
            && !configuration.GetValue<bool>("Oidc:AllowMissingClientSecret"))
        {
            if (!string.Equals(configuration["Environment"], "Development", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Oidc:ClientSecret must be set through secure configuration.");
        }
        var authority = configuration["Oidc:Authority"] ?? "https://localhost:7149";
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = useTestAuthentication
                    ? DemoAuthenticationHandler.SchemeName
                    : CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = useTestAuthentication
                    ? DemoAuthenticationHandler.SchemeName
                    : CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = useTestAuthentication
                    ? DemoAuthenticationHandler.SchemeName
                    : OpenIdConnectDefaults.AuthenticationScheme;
            });
        if (useTestAuthentication)
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, _ => { });
        }
        services.AddAuthentication()
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Home/Forbidden";
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = authority;
                options.ClientId = "accessrequesthub-web";
                options.ClientSecret = configuration["Oidc:ClientSecret"];

                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("roles");

                // Ambil klaim khusus dari respons UserInfo.
                options.ClaimActions.MapUniqueJsonKey(
                    "arh_user_id", "arh_user_id");
                options.ClaimActions.MapUniqueJsonKey(
                    "role", "role");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                // Periksa setelah klaim UserInfo selesai dipetakan.
                options.Events.OnTicketReceived = context =>
                {
                    var localUserId = context.Principal?
                        .FindFirst("arh_user_id")?.Value;

                    if (string.IsNullOrWhiteSpace(localUserId))
                    {
                        context.Fail(
                            "Identity provider did not issue the local user ID claim.");
                    }

                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("Approvers", policy => policy.RequireRole("Manager", "SystemOwner"));
        });

        // View apa pun boleh membaca angka versi terbaru (token concurrency).
        services.AddControllersWithViews(options =>
            {
                options.Filters.Add<DomainExceptionFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            });

        return services;
    }
}
