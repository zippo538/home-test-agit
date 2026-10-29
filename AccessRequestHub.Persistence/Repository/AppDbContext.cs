using AccessRequestHub.Models;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Persistence.Repository;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).IsRequired().HasMaxLength(100);
            e.HasOne(u => u.Manager)
                .WithMany(u => u.DirectReports)
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasData(
                new User { Id = 1, Email = "alice@example.local", DisplayName = "Alice", ManagerId = 2 },
                new User { Id = 2, Email = "bob@example.local", DisplayName = "Bob" },
                new User { Id = 3, Email = "carol@example.local", DisplayName = "Carol" },
                new User { Id = 4, Email = "dana@example.local", DisplayName = "Dana" },
                new User { Id = 5, Email = "erin@example.local", DisplayName = "Erin", IsAuditor = true });
        });

        modelBuilder.Entity<Application>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(a => a.Name).IsUnique();
            e.HasOne(a => a.SystemOwner)
                .WithMany(u => u.OwnedApplications)
                .HasForeignKey(a => a.SystemOwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasData(
                new Application { Id = 1, Name = "CRM", SystemOwnerId = 3 },
                new Application { Id = 2, Name = "Finance Portal", SystemOwnerId = 4 });
        });

        modelBuilder.Entity<PolicyVersion>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Code).IsRequired().HasMaxLength(20);
            e.HasIndex(p => p.Code).IsUnique();
            e.HasData(new PolicyVersion { Id = 1, Code = "v1", IsActive = true });
        });

        modelBuilder.Entity<AccessRequest>(e =>
        {
            e.HasKey(r => r.Id);

            e.Property(r => r.Environment).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(r => r.AccessLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(r => r.Justification).IsRequired().HasMaxLength(2000);
            e.Property(r => r.RejectReason).HasMaxLength(2000);
            e.Property(r => r.Version).IsRequired();

            e.HasOne(r => r.Requester).WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Application).WithMany().HasForeignKey(r => r.ApplicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.PolicyVersion).WithMany().HasForeignKey(r => r.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);

            // Idempotency: dijaga database, bukan hanya aplikasi (D-05, D-06).
            e.HasIndex(r => new { r.RequesterId, r.ClientRequestId }).IsUnique();
            e.HasIndex(r => new { r.Status, r.ApplicationId });
            e.HasIndex(r => r.RequesterId);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AccessRequests_Environment", "Environment IN ('NonProduction','Production')");
                t.HasCheckConstraint("CK_AccessRequests_AccessLevel", "AccessLevel IN ('Read','Admin')");
                t.HasCheckConstraint("CK_AccessRequests_Status", "Status IN ('PendingManager','PendingSystemOwner','Approved','Rejected')");
                t.HasCheckConstraint("CK_AccessRequests_Justification", "length(trim(Justification)) > 0");
                // coalesce wajib: di SQLite, CHECK yang bernilai NULL dianggap lolos.
                t.HasCheckConstraint("CK_AccessRequests_RejectReason",
                    "Status <> 'Rejected' OR length(trim(coalesce(RejectReason, ''))) > 0");
                t.HasCheckConstraint("CK_AccessRequests_Version", "Version >= 1");
            });
        });

        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(a => a.PolicyVersionCode).IsRequired().HasMaxLength(20);
            e.Property(a => a.Reason).HasMaxLength(2000);

            e.HasOne(a => a.Request).WithMany(r => r.AuditEvents).HasForeignKey(a => a.RequestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.RequestId, a.OccurredAt });

            e.ToTable(t => t.HasCheckConstraint("CK_AuditEvents_Action",
                "Action IN ('Created','ManagerApproved','SystemOwnerApproved','Rejected')"));
        });
    }
}
