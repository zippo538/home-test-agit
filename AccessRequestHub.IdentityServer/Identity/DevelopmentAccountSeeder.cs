using AccessRequestHub.Persistence.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.IdentityServer.Identity;

public static class DevelopmentAccountSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUserAccount>>();
        var domainDb = services.GetRequiredService<AppDbContext>();

        var accounts = await domainDb.Users.AsNoTracking().OrderBy(user => user.Id).ToListAsync();
        foreach (var user in accounts)
        {
            var password = configuration[$"DevelopmentAccounts:Passwords:{user.Email}"];
            if (string.IsNullOrWhiteSpace(password))
                continue;

            var account = await userManager.FindByEmailAsync(user.Email);
            if (account is null)
            {
                account = new IdentityUserAccount
                {
                    UserName = user.Email,
                    Email = user.Email,
                    EmailConfirmed = true,
                    DomainUserId = user.Id
                };
                var result = await userManager.CreateAsync(account, password);
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Tidak dapat membuat akun dev {user.Email}: {string.Join(", ", result.Errors.Select(error => error.Code))}");
            }
            else if (account.DomainUserId != user.Id)
            {
                throw new InvalidOperationException($"Mapping akun {user.Email} ke pengguna domain tidak cocok.");
            }
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            var resetEmail = configuration["DevelopmentAccounts:ResetEmail"];
            var resetAll = configuration.GetValue<bool>("DevelopmentAccounts:ResetAllPasswords");


            if (environment.IsDevelopment() && (resetAll || string.Equals(user.Email, resetEmail, StringComparison.OrdinalIgnoreCase)))
            {
                void EnsureSuccess(IdentityResult result, string operation)
                {
                    if (!result.Succeeded)
                    {
                        var errors = string.Join(", ",
                            result.Errors.Select(error => error.Code));

                        throw new InvalidOperationException(
                            $"{operation} gagal untuk {user.Email}: {errors}");
                    }
                }

                var token = await userManager.GeneratePasswordResetTokenAsync(account);

                var resetResult = await userManager.ResetPasswordAsync(
                    account, token, password);
                EnsureSuccess(resetResult, "Reset password");

                var unlockResult = await userManager.SetLockoutEndDateAsync(
                    account, DateTimeOffset.UtcNow.AddMinutes(-1));
                EnsureSuccess(unlockResult, "Buka kunci akun");

                var failedCountResult =
                    await userManager.ResetAccessFailedCountAsync(account);
                EnsureSuccess(failedCountResult, "Reset jumlah kegagalan login");
            }

            var roles = new List<string> { "Requester" };
            if (user.DirectReports.Count > 0) roles.Add("Manager");
            if (user.OwnedApplications.Count > 0) roles.Add("SystemOwner");
            if (user.IsAuditor) roles.Add("Admin");
            foreach (var role in roles)
            {
                if (!await userManager.IsInRoleAsync(account, role))
                {
                    var result = await userManager.AddToRoleAsync(account, role);
                    if (!result.Succeeded)
                        throw new InvalidOperationException($"Tidak dapat menambahkan role {role} ke {user.Email}.");
                }
            }
        }
    }
}
