using System.Net;
using AccessRequestHub.Tests.Infrastructure;
using home_test_agit.Models;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Tests.Integration;

/// <summary>AC-01, AC-02, AC-03, AC-07, AC-17: alur bisnis end-to-end lewat HTTP.</summary>
public class WorkflowTests : IntegrationTest
{
    [Fact] // AC-01 Standard request
    public async Task Standard_request_langsung_approved_setelah_manager_approve()
    {
        var alice = As(Alice);
        var id = await CreateRequestAsync(alice, CrmAppId, TargetEnvironment.NonProduction, AccessLevel.Read);

        // Request baru selalu menunggu Manager (AC-17: high-risk tersimpan saat create).
        Assert.Equal("PendingManager", await StatusOfAsync(id));
        Assert.False(await QueryAsync(db => db.AccessRequests.Where(r => r.Id == id).Select(r => r.IsHighRisk).SingleAsync()));

        var response = await ApproveAsync(As(Bob), id, version: 1);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("Approved", await StatusOfAsync(id));
        Assert.Equal(2, await VersionOfAsync(id));
        Assert.Equal(new[] { "Created", "ManagerApproved" }, await AuditActionsAsync(id));
    }

    [Fact] // AC-02 Production request
    public async Task Production_request_butuh_manager_lalu_system_owner()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.Production, AccessLevel.Read);

        Assert.True(await QueryAsync(db => db.AccessRequests.Where(r => r.Id == id).Select(r => r.IsHighRisk).SingleAsync()));

        var managerResponse = await ApproveAsync(As(Bob), id, version: 1);

        Assert.Equal(HttpStatusCode.Redirect, managerResponse.StatusCode);
        Assert.Equal("PendingSystemOwner", await StatusOfAsync(id));

        var ownerResponse = await ApproveAsync(As(Carol), id, version: 2);

        Assert.Equal(HttpStatusCode.Redirect, ownerResponse.StatusCode);
        Assert.Equal("Approved", await StatusOfAsync(id));
        Assert.Equal(new[] { "Created", "ManagerApproved", "SystemOwnerApproved" }, await AuditActionsAsync(id));
    }

    [Fact] // AC-03 Admin access -> Dana (bukan Carol)
    public async Task Admin_access_finance_portal_menunggu_Dana()
    {
        var id = await CreateRequestAsync(As(Alice), FinanceAppId, TargetEnvironment.NonProduction, AccessLevel.Admin);

        await ApproveAsync(As(Bob), id, version: 1);
        Assert.Equal("PendingSystemOwner", await StatusOfAsync(id));

        // Carol adalah owner CRM, bukan Finance Portal: tidak boleh melihat maupun memproses.
        Assert.Equal(HttpStatusCode.NotFound, (await As(Carol).GetAsync($"/Requests/Details/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ApproveAsync(As(Carol), id, version: 2)).StatusCode);

        // Dana adalah owner Finance Portal.
        var dana = await As(Dana).GetStringAsync($"/Requests/Details/{id}");
        Assert.Contains("Finance Portal", dana);

        Assert.Equal(HttpStatusCode.Redirect, (await ApproveAsync(As(Dana), id, version: 2)).StatusCode);
        Assert.Equal("Approved", await StatusOfAsync(id));
    }

    [Fact] // AC-07 Rejected request
    public async Task Reject_menyimpan_reason_dan_mengunci_request()
    {
        var id = await CreateRequestAsync(As(Alice));

        var response = await RejectAsync(As(Bob), id, version: 1, reason: "Tidak ada kebutuhan bisnis");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("Rejected", await StatusOfAsync(id));
        Assert.Equal("Tidak ada kebutuhan bisnis",
            await QueryAsync(db => db.AccessRequests.Where(r => r.Id == id).Select(r => r.RejectReason).SingleAsync()));

        var audit = await QueryAsync(db => db.AuditEvents
            .Where(a => a.RequestId == id && a.Action == AuditAction.Rejected)
            .Select(a => a.Reason).SingleAsync());
        Assert.Equal("Tidak ada kebutuhan bisnis", audit);

        // AC-09: estado terminal tidak bisa diproses lagi.
        Assert.Equal(HttpStatusCode.Conflict, (await ApproveAsync(As(Bob), id, version: 2)).StatusCode);
        Assert.Equal("Rejected", await StatusOfAsync(id));
    }

    [Fact] // AC-13: Manager hanya direct report, System Owner hanya aplikasinya
    public async Task Inbox_berisi_request_yang_sesuai_peran()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.NonProduction, AccessLevel.Read);

        var bobInbox = await As(Bob).GetStringAsync("/Approvals");
        Assert.Contains("Alice", bobInbox);

        // Carol bukan manager Alice dan belum gilirannya: inbox kosong.
        var carolInbox = await As(Carol).GetStringAsync("/Approvals");
        Assert.Contains("Tidak ada request yang menunggu Anda", carolInbox);

        Assert.Equal("PendingManager", await StatusOfAsync(id));
    }

    [Fact] // AC-18: auditor melihat semua dan bukan approver
    public async Task Auditor_melihat_semua_tetapi_tidak_bisa_approve()
    {
        var id = await CreateRequestAsync(As(Alice));

        var all = await As(Erin).GetStringAsync("/Requests/All");
        Assert.Contains("CRM", all);

        Assert.Equal(HttpStatusCode.Forbidden, (await ApproveAsync(As(Erin), id, version: 1)).StatusCode);
        Assert.Equal("PendingManager", await StatusOfAsync(id));
        Assert.Equal(1, await AuditCountAsync(id)); // tidak ada audit event tambahan
    }
}
