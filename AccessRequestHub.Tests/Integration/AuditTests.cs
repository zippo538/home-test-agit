using AccessRequestHub.Tests.Infrastructure;
using AccessRequestHub.Models;
using AccessRequestHub.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Tests.Integration;

/// <summary>AC-15, AC-16, AC-17: audit atomic, append-only, dan snapshot policy.</summary>
public class AuditTests : IntegrationTest
{
    [Fact] // AC-17
    public async Task Audit_menyimpan_actor_aksi_transisi_version_dan_policy()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.Production, AccessLevel.Read);
        await ApproveAsync(As(Bob), id, version: 1);

        var events = await QueryAsync(db => db.AuditEvents
            .Where(a => a.RequestId == id).OrderBy(a => a.Id).ToListAsync());

        Assert.Equal(2, events.Count);

        var created = events[0];
        Assert.Equal(AuditAction.Created, created.Action);
        Assert.Equal(AliceId, created.ActorId);
        Assert.Null(created.FromStatus);
        Assert.Equal(RequestStatus.PendingManager, created.ToStatus);
        Assert.Equal(1, created.RequestVersion);
        Assert.Equal("v1", created.PolicyVersionCode);

        var approved = events[1];
        Assert.Equal(AuditAction.ManagerApproved, approved.Action);
        Assert.Equal(BobId, approved.ActorId);
        Assert.Equal(RequestStatus.PendingManager, approved.FromStatus);
        Assert.Equal(RequestStatus.PendingSystemOwner, approved.ToStatus);
        Assert.Equal(2, approved.RequestVersion);
    }

    [Fact] // AC-16
    public async Task UPDATE_pada_AuditEvents_ditolak_database()
    {
        var id = await CreateRequestAsync(As(Alice));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => QueryAsync(async db =>
            await db.Database.ExecuteSqlRawAsync("UPDATE AuditEvents SET Action = 'Created' WHERE RequestId = {0}", id)));

        Assert.Contains("append-only", ex.Message);
    }

    [Fact] // AC-16
    public async Task DELETE_pada_AuditEvents_ditolak_database()
    {
        var id = await CreateRequestAsync(As(Alice));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => QueryAsync(async db =>
            await db.Database.ExecuteSqlRawAsync("DELETE FROM AuditEvents WHERE RequestId = {0}", id)));

        Assert.Contains("append-only", ex.Message);
        Assert.Equal(1, await AuditCountAsync(id)); // baris tetap utuh
    }

    [Fact] // AC-05 + AC-15: retry tidak menulis audit kedua
    public async Task Retry_tidak_menambah_audit_event()
    {
        var clientRequestId = Guid.NewGuid();
        var first = await CreateRequestAsync(As(Alice), clientRequestId: clientRequestId);
        await CreateRequestAsync(As(Alice), clientRequestId: clientRequestId);

        Assert.Equal(1, await AuditCountAsync(first));
    }
}
