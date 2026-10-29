using System.Security.Claims;
using Duende.IdentityServer.AspNetIdentity;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using AccessRequestHub.Persistence.Repository;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.IdentityServer.Identity;

public sealed class DomainClaimsProfileService(
    UserManager<IdentityUserAccount> userManager,
    IUserClaimsPrincipalFactory<IdentityUserAccount> claimsFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<ProfileService<IdentityUserAccount>> logger)
    : ProfileService<IdentityUserAccount>(userManager, claimsFactory, logger)
{
    public override async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken ct = default)
    {
        await base.GetProfileDataAsync(context, ct);
        var subject = context.Subject.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
            return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await userManager.FindByIdAsync(subject);
        if (account is null)
            return;

        var user = await db.Users.AsNoTracking()
            .Where(item => item.Id == account.DomainUserId)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Email,
                item.IsAuditor,
                IsManager = item.DirectReports.Any(),
                IsSystemOwner = item.OwnedApplications.Any()
            })
            .SingleOrDefaultAsync();
        if (user is null)
            return;

        var claims = new List<Claim>
        {
            new("arh_user_id", user.Id.ToString()),
            new("name", user.DisplayName),
            new("email", user.Email),
            new("role", "Requester")
        };
        if (user.IsManager) claims.Add(new Claim("role", "Manager"));
        if (user.IsSystemOwner) claims.Add(new Claim("role", "SystemOwner"));
        if (user.IsAuditor) claims.Add(new Claim("role", "Admin"));

        context.IssuedClaims.RemoveAll(claim => claim.Type == "role"
            || claim.Type == "arh_user_id" || claim.Type == "name"
            || claim.Type == "email" || claim.Type == "is_auditor");
        context.IssuedClaims.AddRange(claims);
    }

    public override Task IsActiveAsync(IsActiveContext context, CancellationToken ct = default)
        => base.IsActiveAsync(context, ct);
}
