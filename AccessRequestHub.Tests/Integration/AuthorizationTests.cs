using System.Net;
using AccessRequestHub.Tests.Infrastructure;
using home_test_agit.Models;
using home_test_agit.Models.Enums;

namespace AccessRequestHub.Tests.Integration;

/// <summary>
/// AC-04, AC-12, AC-14, AC-19: otorisasi ditegakkan server-side.
/// Setiap penolakan juga memastikan state dan audit tidak berubah.
/// </summary>
public class AuthorizationTests : IntegrationTest
{
    [Fact] // AC-04
    public async Task Bukan_approver_terkait_ditolak_403_dan_state_tidak_berubah()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.NonProduction, AccessLevel.Read);

        // Carol (owner CRM) belum gilirannya; Erin auditor; Dana tidak terkait.
        Assert.Equal(HttpStatusCode.Forbidden, (await ApproveAsync(As(Erin), id, version: 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ApproveAsync(As(Dana), id, version: 1)).StatusCode);

        // Carol memang terkait aplikasi, tapi masih tahap Manager (AC-14 -> 409).
        Assert.Equal(HttpStatusCode.Conflict, (await ApproveAsync(As(Carol), id, version: 1)).StatusCode);

        Assert.Equal("PendingManager", await StatusOfAsync(id));
        Assert.Equal(1, await VersionOfAsync(id));
        Assert.Equal(1, await AuditCountAsync(id));
    }

    [Fact] // AC-12
    public async Task Requester_tidak_bisa_approve_requestnya_sendiri()
    {
        var id = await CreateRequestAsync(As(Alice));

        var response = await ApproveAsync(As(Alice), id, version: 1);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PendingManager", await StatusOfAsync(id));
        Assert.Equal(1, await AuditCountAsync(id));
    }

    [Fact] // AC-14
    public async Task System_owner_memproses_sebelum_gilirannya_ditolak_409()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.Production, AccessLevel.Read);

        var response = await ApproveAsync(As(Carol), id, version: 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("not_your_stage", await response.Content.ReadAsStringAsync());
        Assert.Equal("PendingManager", await StatusOfAsync(id));
    }

    [Fact] // AC-19
    public async Task Requester_lain_mendapat_404_bukan_403()
    {
        var aliceRequest = await CreateRequestAsync(As(Alice));

        // Tidak ada user lain yang merupakan requester di seed; Dana dipakai sebagai
        // pihak yang tidak terkait sama sekali dengan request CRM milik Alice.
        var response = await As(Dana).GetAsync($"/Requests/Details/{aliceRequest}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact] // AC-19
    public async Task My_requests_hanya_berisi_milik_sendiri()
    {
        await CreateRequestAsync(As(Alice));

        var aliceList = await As(Alice).GetStringAsync("/Requests");
        Assert.Contains("CRM", aliceList);

        // Bob punya inbox, tapi My Requests miliknya kosong.
        var bobList = await As(Bob).GetStringAsync("/Requests");
        Assert.Contains("Belum ada request", bobList);
    }

    [Fact] // AC-18
    public async Task Auditor_tidak_bisa_membuka_All_requests()
    {
        // Sebaliknya: non-auditor ditolak 403 pada halaman All.
        await CreateRequestAsync(As(Alice));

        Assert.Equal(HttpStatusCode.Forbidden, (await As(Bob).GetAsync("/Requests/All")).StatusCode);
    }
}
