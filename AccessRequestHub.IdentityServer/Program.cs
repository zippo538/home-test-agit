using AccessRequestHub.IdentityServer.Identity;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using AccessRequestHub.Persistence.Data;
using AccessRequestHub.Persistence.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

var identityConnection = builder.Configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("ConnectionStrings:Identity is required.");
var hubConnection = builder.Configuration.GetConnectionString("Hub")
    ?? throw new InvalidOperationException("ConnectionStrings:Hub is required.");
var clientSecret = builder.Configuration["Oidc:ClientSecret"];
if (string.IsNullOrWhiteSpace(clientSecret))
    throw new InvalidOperationException("Oidc:ClientSecret must be supplied using user secrets or environment configuration.");

builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(identityConnection));
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(hubConnection));
builder.Services.AddIdentity<IdentityUserAccount, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddControllersWithViews()
    .ConfigureApplicationPartManager(manager =>
    {
        var mvcAssemblyName = typeof(AppDbContext).Assembly.GetName().Name;
        var mvcApplicationPart = manager.ApplicationParts.FirstOrDefault(part => part.Name == mvcAssemblyName);
        if (mvcApplicationPart is not null)
            manager.ApplicationParts.Remove(mvcApplicationPart);
    });

var microsoftClientId = builder.Configuration["Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Microsoft:ClientSecret"];
var microsoftConfigured = !string.IsNullOrWhiteSpace(microsoftClientId)
    && !string.IsNullOrWhiteSpace(microsoftClientSecret);

var authentication = builder.Services.AddAuthentication();
if (microsoftConfigured)
{
    authentication.AddOpenIdConnect(MicrosoftExternalLoginLinker.SchemeName, options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Authority = "https://login.microsoftonline.com/consumers/v2.0";
        options.ClientId = microsoftClientId!;
        options.ClientSecret = microsoftClientSecret!;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.CallbackPath = "/signin-microsoft";
        options.MapInboundClaims = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/Account/Login?error=external");
            return Task.CompletedTask;
        };
    });
}
builder.Services.AddScoped<MicrosoftExternalLoginLinker>();

builder.Services.AddIdentityServer(options =>
    {
        options.EmitStaticAudienceClaim = true;
        var licenseKey = builder.Configuration["Duende:LicenseKey"];
        if (!string.IsNullOrWhiteSpace(licenseKey))
            options.LicenseKey = licenseKey;
    })
    .AddAspNetIdentity<IdentityUserAccount>()
    .AddProfileService<DomainClaimsProfileService>()
    .AddInMemoryIdentityResources(
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        new IdentityResource("roles", ["role", "arh_user_id"])
    ])
    .AddInMemoryApiScopes([new ApiScope("accessrequesthub.api", "Access Request Hub API")])
    .AddInMemoryClients(
    [
        new Client
        {
            ClientId = "accessrequesthub-web",
            ClientName = "Access Request Hub MVC",
            ClientSecrets = [new Secret(clientSecret.Sha256())],
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireConsent = false,
            RedirectUris = ["http://localhost:5135/signin-oidc", "https://localhost:7019/signin-oidc"],
            PostLogoutRedirectUris = ["http://localhost:5135/signout-callback-oidc", "https://localhost:7019/signout-callback-oidc"],
            AllowedScopes = [IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email, "roles"],
            AllowOfflineAccess = false
        }
    ]);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    if (string.IsNullOrWhiteSpace(builder.Configuration["Duende:LicenseKey"]))
        throw new InvalidOperationException("Duende:LicenseKey is required outside Development.");
}

await DatabaseBootstrapper.InitializeAsync(app.Services);
using (var scope = app.Services.CreateScope())
{
    await IdentityDatabaseBootstrapper.InitializeAsync(scope.ServiceProvider);
    if (app.Environment.IsDevelopment())
        await DevelopmentAccountSeeder.SeedAsync(scope.ServiceProvider, builder.Configuration);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Account}/{action=Login}/{id?}");
app.Run();

public partial class Program;