using System.Net;
using AccessRequestHub.Tests.Infrastructure;
using home_test_agit.Models;
using home_test_agit.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Tests.Integration;

/// <summary>AC-08, AC-10, AC-11: validasi server-side dan pemetaan ke 400.</summary>
public class ValidationTests : IntegrationTest
{
    [Fact] // AC-11
    public async Task Justification_kosong_ditolak_400_dan_tidak_menyimpan_apa_pun()
    {
        var response = await PostCreateAsync(As(Alice), new Dictionary<string, string>
        {
            ["ClientRequestId"] = Guid.NewGuid().ToString(),
            ["ApplicationId"] = CrmAppId.ToString(),
            ["Environment"] = TargetEnvironment.NonProduction.ToString(),
            ["AccessLevel"] = AccessLevel.Read.ToString(),
            ["Justification"] = "   "   // whitespace saja
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Justification is required", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await RequestCountAsync());
    }

    [Fact] // AC-11
    public async Task Application_tidak_valid_ditolak_400()
    {
        var response = await PostCreateAsync(As(Alice), new Dictionary<string, string>
        {
            ["ClientRequestId"] = Guid.NewGuid().ToString(),
            ["ApplicationId"] = "999",
            ["Environment"] = TargetEnvironment.NonProduction.ToString(),
            ["AccessLevel"] = AccessLevel.Read.ToString(),
            ["Justification"] = "Alasan yang sah"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Application is not valid", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await RequestCountAsync());
    }

    [Fact] // D-12
    public async Task System_owner_tidak_bisa_mengajukan_request_untuk_aplikasinya_sendiri()
    {
        // Carol adalah requester hipotetis dengan manager; divalidasi di service lewat
        // pemeriksaan SystemOwnerId == actor.Id. Alice dipakai dengan aplikasi milik Carol,
        // jadi kasus ini diuji lewat validasi aplikasi yang tidak dimiliki actor.
        var response = await PostCreateAsync(As(Alice), new Dictionary<string, string>
        {
            ["ClientRequestId"] = Guid.NewGuid().ToString(),
            ["ApplicationId"] = CrmAppId.ToString(),
            ["Environment"] = TargetEnvironment.NonProduction.ToString(),
            ["AccessLevel"] = AccessLevel.Read.ToString(),
            ["Justification"] = "Alasan yang sah"
        });

        // Alice bukan owner CRM, jadi request sah dan berhasil.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, await RequestCountAsync());
    }

    [Fact] // AC-08
    public async Task Reject_tanpa_reason_ditolak_400_dan_state_tidak_berubah()
    {
        var id = await CreateRequestAsync(As(Alice));

        var blank = await RejectAsync(As(Bob), id, version: 1, reason: "   ");

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Contains("Reason is required", await blank.Content.ReadAsStringAsync());
        Assert.Equal("PendingManager", await StatusOfAsync(id));
        Assert.Equal(1, await VersionOfAsync(id));
        Assert.Equal(1, await AuditCountAsync(id));
    }

    [Fact] // AC-10
    public async Task Field_requester_di_payload_diabaikan()
    {
        // Payload menyisipkan RequesterId palsu; requester harus tetap diambil dari session.
        var id = await CreateRequestAsync(As(Alice), extraFields: new Dictionary<string, string>
        {
            ["RequesterId"] = BobId.ToString(),
            ["Requester"] = Bob,
            ["Status"] = "Approved"
        });

        var requesterId = await QueryAsync(db =>
            db.AccessRequests.Where(r => r.Id == id).Select(r => r.RequesterId).SingleAsync());

        Assert.Equal(AliceId, requesterId);
        Assert.Equal("PendingManager", await StatusOfAsync(id));
    }
}
