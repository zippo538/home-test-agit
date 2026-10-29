using System.Globalization;
using System.Security.Claims;
using AccessRequestHub.Persistence.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.IdentityServer.Identity;

public enum ExternalLoginOutcome
{
    Success,
    RequiresApproval,
    Failure
}

public sealed record ExternalLoginResolution(
    ExternalLoginOutcome Outcome,
    IdentityUserAccount? Account = null,
    string? Subject = null);

/// <summary>
/// Links a verified Microsoft subject to one existing domain user only when an operator
/// explicitly configures Microsoft:ApprovedSubjects:{subject} with that user's numeric ID.
/// Email claims are never used to select or link an account.
/// </summary>
public sealed class MicrosoftExternalLoginLinker(
    UserManager<IdentityUserAccount> userManager,
    AppDbContext hubDb,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<MicrosoftExternalLoginLinker> logger)
{
    public const string SchemeName = "Microsoft";
    public const string LoginProvider = "Microsoft";

    public static string? ExtractSubject(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(subject))
            return subject;

        subject = principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }

    public async Task<ExternalLoginResolution> ResolveAsync(string subject, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return new ExternalLoginResolution(ExternalLoginOutcome.Failure);

        var loginKey = new UserLoginInfo(LoginProvider, subject, LoginProvider);
        var existingAccount = await userManager.FindByLoginAsync(loginKey.LoginProvider, loginKey.ProviderKey);
        var configuredDomainUserId = configuration[$"Microsoft:ApprovedSubjects:{subject}"];
        logger.LogInformation(
            "Microsoft mapping: ConfiguredDomainUserId={ConfiguredId}, ExistingDomainUserId={ExistingId}",
            configuredDomainUserId,
            existingAccount?.DomainUserId);

        if (existingAccount is not null)
        {
            if (!string.IsNullOrWhiteSpace(configuredDomainUserId)
                && (!int.TryParse(configuredDomainUserId, NumberStyles.None, CultureInfo.InvariantCulture, out var configuredId)
                    || configuredId != existingAccount.DomainUserId))
                return new ExternalLoginResolution(ExternalLoginOutcome.Failure, null, subject);

            return new ExternalLoginResolution(ExternalLoginOutcome.Success, existingAccount, subject);
        }

        if (string.IsNullOrWhiteSpace(configuredDomainUserId))
            return environment.IsDevelopment()
                ? new ExternalLoginResolution(ExternalLoginOutcome.RequiresApproval, null, subject)
                : new ExternalLoginResolution(ExternalLoginOutcome.Failure, null, subject);

        if (!int.TryParse(configuredDomainUserId, NumberStyles.None, CultureInfo.InvariantCulture, out var domainUserId))
            return new ExternalLoginResolution(ExternalLoginOutcome.Failure, null, subject);

        var domainUserExists = await hubDb.Users.AsNoTracking()
            .AnyAsync(user => user.Id == domainUserId, ct);
        logger.LogInformation(
            "Microsoft mapping: DomainUserExists={Exists}",
            domainUserExists);
        if (!domainUserExists)
            return new ExternalLoginResolution(ExternalLoginOutcome.Failure, null, subject);



        var account = await userManager.Users
            .SingleOrDefaultAsync(user => user.DomainUserId == domainUserId, ct);
        logger.LogInformation(
            "Microsoft mapping: IdentityAccountExists={Exists}",
            account is not null);
        if (account is null)
            return new ExternalLoginResolution(ExternalLoginOutcome.Failure, null, subject);

        // Avoid silently adding a Microsoft login where this local account has another external provider.
        var existingLogins = await userManager.GetLoginsAsync(account);
        if (existingLogins.Count != 0)
            return new ExternalLoginResolution(ExternalLoginOutcome.Failure, null, subject);

        var addResult = await userManager.AddLoginAsync(account, loginKey);
        if (!addResult.Succeeded)
        {
            foreach (var error in addResult.Errors)
            {
                logger.LogWarning(
                    "Microsoft mapping: AddLogin failed, Code={Code}",
                    error.Code);
            }
            return new ExternalLoginResolution(
                ExternalLoginOutcome.Failure, null, subject);
        }

        return new ExternalLoginResolution(ExternalLoginOutcome.Success, account, subject);
    }
}
