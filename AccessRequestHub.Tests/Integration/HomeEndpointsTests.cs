using System.Net;
using AccessRequestHub.Tests.Infrastructure;

namespace AccessRequestHub.Tests.Integration;

/// <summary>
/// R-06: target error yang dipakai Program.cs/AccessDeniedPath harus benar-benar ada.
/// </summary>
public class HomeEndpointsTests : IntegrationTest
{
    [Fact]
    public async Task Error_page_merender_500_tanpa_redirect()
    {
        var response = await As(Alice).GetAsync("/Home/Error");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Error", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Forbidden_page_merender_403()
    {
        var response = await As(Alice).GetAsync("/Home/Forbidden");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Forbidden", await response.Content.ReadAsStringAsync());
    }
}
