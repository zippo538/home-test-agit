using Microsoft.AspNetCore.Identity;

namespace AccessRequestHub.IdentityServer.Identity;

public sealed class IdentityUserAccount : IdentityUser
{
    public int DomainUserId { get; set; }
}
