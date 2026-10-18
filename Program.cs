using home_test_agit.Data;
using home_test_agit.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAccessRequestHub(
    builder.Configuration.GetConnectionString("Default")!,
    builder.Configuration);

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
