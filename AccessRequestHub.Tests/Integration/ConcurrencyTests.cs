using System.Net;
using AccessRequestHub.Tests.Infrastructure;
using home_test_agit.Models;

namespace AccessRequestHub.Tests.Integration;

/// <summary>AC-06: optimistic concurrency. Hanya satu transisi per version yang berhasil.</summary>
public class ConcurrencyTests : IntegrationTest
{
    [Fact]
    public async Task Approve_dengan_version_lama_ditolak_409_stale_version()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.Production, AccessLevel.Read);

        // Bob approve version 1 -> naik ke version 2.
        await ApproveAsync(As(Bob), id, version: 1);
        Assert.Equal(2, await VersionOfAsync(id));

        // Carol masih memakai version 1 yang sudah basi.
        var stale = await ApproveAsync(As(Carol), id, version: 1);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("stale_version", await stale.Content.ReadAsStringAsync());

        // State tidak bergeser dan tidak ada audit event tambahan.
        Assert.Equal("PendingSystemOwner", await StatusOfAsync(id));
        Assert.Equal(2, await VersionOfAsync(id));
        Assert.Equal(2, await AuditCountAsync(id));
    }

    [Fact]
    public async Task Dua_approve_paralel_dengan_version_sama_satu_berhasil_satu_konflik()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.Production, AccessLevel.Read);

        // Dua System Owner berbeda tidak ada di seed, jadi dipakai Manager vs request yang
        // sudah di tahap System Owner: satu aktor sah dan satu aktor yang sudah basi.
        await ApproveAsync(As(Bob), id, version: 1);
        Assert.Equal(2, await VersionOfAsync(id));

        var first = As(Carol);
        var second = As(Carol);
        await first.GetAsync($"/Requests/Details/{id}");

        var responses = await Task.WhenAll(
            ApproveAsync(first, id, version: 2),
            ApproveAsync(second, id, version: 2));

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Redirect);
        var conflicted = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, succeeded);
        Assert.Equal(1, conflicted);

        // Version naik tepat satu, bukan dua: tidak ada transisi ganda.
        Assert.Equal(3, await VersionOfAsync(id));
        Assert.Equal("Approved", await StatusOfAsync(id));
        Assert.Equal(3, await AuditCountAsync(id));
    }

    [Fact] // AC-09
    public async Task Approve_ulang_setelah_approved_ditolak_409_already_final()
    {
        var id = await CreateRequestAsync(As(Alice), CrmAppId, TargetEnvironment.NonProduction, AccessLevel.Read);

        await ApproveAsync(As(Bob), id, version: 1);
        Assert.Equal("Approved", await StatusOfAsync(id));

        var again = await ApproveAsync(As(Bob), id, version: 2);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already_final", await again.Content.ReadAsStringAsync());
        Assert.Equal(2, await VersionOfAsync(id));
        Assert.Equal(2, await AuditCountAsync(id));
    }
}
