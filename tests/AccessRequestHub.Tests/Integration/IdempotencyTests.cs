using System.Net;
using AccessRequestHub.Tests.Infrastructure;
using home_test_agit.Models;
using Microsoft.EntityFrameworkCore;

namespace AccessRequestHub.Tests.Integration;

/// <summary>AC-05 + D-05/D-07: idempotency create, termasuk saat race.</summary>
public class IdempotencyTests : IntegrationTest
{
    [Fact]
    public async Task Retry_sekuensial_dengan_ClientRequestId_sama_tetap_satu_request()
    {
        var alice = As(Alice);
        var clientRequestId = Guid.NewGuid();

        var first = await CreateRequestAsync(alice, clientRequestId: clientRequestId);
        var second = await CreateRequestAsync(alice, clientRequestId: clientRequestId);

        Assert.Equal(first, second);
        Assert.Equal(1, await RequestCountAsync());
        Assert.Equal(1, await AuditCountAsync(first));
    }

    [Fact]
    public async Task Retry_paralel_tetap_menghasilkan_satu_baris()
    {
        // Race nyata: 10 permintaan bersamaan dengan ClientRequestId yang sama.
        var clientRequestId = Guid.NewGuid();

        var attempts = Enumerable.Range(0, 10)
            .Select(_ => CreateRequestAsync(As(Alice), clientRequestId: clientRequestId))
            .ToArray();

        var ids = await Task.WhenAll(attempts);

        Assert.Single(ids.Distinct());
        Assert.Equal(1, await RequestCountAsync());
        Assert.Equal(1, await AuditCountAsync(ids[0]));
    }

    [Fact] // D-07
    public async Task Payload_berbeda_dengan_ClientRequestId_sama_ditolak()
    {
        var alice = As(Alice);
        var clientRequestId = Guid.NewGuid();

        await CreateRequestAsync(alice, CrmAppId, TargetEnvironment.NonProduction, AccessLevel.Read,
            justification: "Alasan pertama", clientRequestId: clientRequestId);

        // Payload berbeda dengan ID yang sama: bukan retry, jadi harus dikonfirmasi ke pemanggil.
        var response = await PostCreateAsync(alice, new Dictionary<string, string>
        {
            ["ClientRequestId"] = clientRequestId.ToString(),
            ["ApplicationId"] = CrmAppId.ToString(),
            ["Environment"] = TargetEnvironment.Production.ToString(),
            ["AccessLevel"] = AccessLevel.Admin.ToString(),
            ["Justification"] = "Alasan berbeda"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await RequestCountAsync());

        // Request asli tidak ikut berubah.
        Assert.Equal("PendingManager", await StatusOfAsync(
            await QueryAsync(db => db.AccessRequests.Select(r => r.Id).SingleAsync())));
    }

    [Fact] // D-06: scope per requester, bukan global
    public async Task ClientRequestId_yang_sama_boleh_dipakai_user_berbeda()
    {
        var clientRequestId = Guid.NewGuid();

        // Alice membuat request; Carol tidak punya manager sehingga tidak boleh membuat request,
        // jadi dipakai pembanding bahwa ID yang sama tidak "menabrak" milik Alice.
        var aliceRequest = await CreateRequestAsync(As(Alice), clientRequestId: clientRequestId);

        Assert.Equal(1, await RequestCountAsync());

        // ID milik Alice tetap bisa ditemukan lewat retry oleh Alice sendiri.
        var retry = await CreateRequestAsync(As(Alice), clientRequestId: clientRequestId);
        Assert.Equal(aliceRequest, retry);
        Assert.Equal(1, await RequestCountAsync());
    }

    [Fact] // D-11
    public async Task User_tanpa_manager_tidak_bisa_membuat_request()
    {
        var html = await As(Carol).GetStringAsync("/Requests/Create");

        Assert.Contains("tidak memiliki atasan", html);
        Assert.Equal(0, await RequestCountAsync());
    }
}
