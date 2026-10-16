using home_test_agit.Auth;
using home_test_agit.Filters;
using home_test_agit.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace home_test_agit.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Mendaftarkan DbContext, repository, service, dan auth simulasi Access Request Hub.</summary>
    public static IServiceCollection AddAccessRequestHub(this IServiceCollection services, string connectionString)
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

        // Auth simulasi berbasis cookie; header X-Demo-User hanya aktif lewat konfigurasi (D-16).
        services
            .AddAuthentication(DemoAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
                DemoAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization();

        // View apa pun boleh membaca angka versi terbaru (token concurrency).
        services.AddControllersWithViews(options =>
            {
                options.Filters.Add<DomainExceptionFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            });

        return services;
    }
}
