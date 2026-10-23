using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.IdentityServer.Identity;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<IdentityUserAccount>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<IdentityUserAccount>().HasIndex(user => user.DomainUserId).IsUnique();
        builder.Entity<IdentityUserAccount>().Property(user => user.DomainUserId).IsRequired();
    }
}
