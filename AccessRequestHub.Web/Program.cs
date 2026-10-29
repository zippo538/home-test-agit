using AccessRequestHub.Persistence.Data;
using AccessRequestHub.Web.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAccessRequestHub(
    builder.Configuration.GetConnectionString("Default")!,
    builder.Configuration);

// Hanya aktif saat dijalankan AppHost. Konfigurasi dijalankan sebelum
// post-configuration OIDC membentuk discovery ConfigurationManager.
var aspireAuthority = builder.Configuration["Aspire:IdentityAuthority"];
if (!string.IsNullOrWhiteSpace(aspireAuthority))
{
    builder.Services.ConfigureAll<OpenIdConnectOptions>(options =>
    {
        options.Authority = aspireAuthority;
        options.MetadataAddress = $"{aspireAuthority.TrimEnd('/')}/.well-known/openid-configuration";
        options.ClientId = "accessrequesthub-web";
        options.ClientSecret = builder.Configuration["Oidc:ClientSecret"]
            ?? throw new InvalidOperationException("Oidc:ClientSecret is required.");
        options.RequireHttpsMetadata = true;
        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
    });
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

var app = builder.Build();

// Schema + seed reproducible: hapus file .db, jalankan aplikasi (AC-21).
await DatabaseBootstrapper.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// UseAuthentication wajib mendahului UseAuthorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Requests}/{action=Index}/{id?}");

app.Run();

/// <summary>Penanda assembly untuk integration test (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;