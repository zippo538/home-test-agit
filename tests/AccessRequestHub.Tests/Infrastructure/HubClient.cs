using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AccessRequestHub.Tests.Infrastructure;

/// <summary>
/// Klien HTTP yang berlaku sebagai satu demo user, termasuk mengurus cookie dan
/// antiforgery token seperti browser sungguhan (AC-10 memastikan identitas dari session).
/// </summary>
public sealed partial class HubClient
{
    private readonly HttpClient _http;
    private string? _token;

    private HubClient(HttpClient http, string email)
    {
        _http = http;
        Email = email;
    }

    public string Email { get; }

    public static HubClient For(HubAppFactory factory, string email)
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,   // redirect dicek eksplisit di test
            HandleCookies = true
        });
        http.DefaultRequestHeaders.Add("X-Demo-User", email);
        return new HubClient(http, email);
    }

    public string EmailLocalPart => Email.Split('@')[0];

    public Task<HttpResponseMessage> GetAsync(string url) => _http.GetAsync(url);

    public async Task<string> GetStringAsync(string url)
        => await (await _http.GetAsync(url)).Content.ReadAsStringAsync();

    /// <summary>POST form dengan antiforgery token yang diambil dari halaman berisi form.</summary>
    public async Task<HttpResponseMessage> PostFormAsync(
        string url, IDictionary<string, string> fields, string tokenPageUrl)
    {
        _token ??= await FetchTokenAsync(tokenPageUrl);

        var payload = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = _token
        };

        return await _http.PostAsync(url, new FormUrlEncodedContent(payload));
    }

    private async Task<string> FetchTokenAsync(string pageUrl)
    {
        var html = await GetStringAsync(pageUrl);
        var match = TokenRegex().Match(html);

        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token tidak ditemukan di {pageUrl}.");

        return match.Groups["value"].Value;
    }

    /// <summary>Mengambil ID request dari Location header setelah redirect sukses.</summary>
    public static Guid RequestIdFrom(HttpResponseMessage response)
    {
        var location = response.Headers.Location
                       ?? throw new InvalidOperationException("Response tidak punya Location header.");

        var match = GuidRegex().Match(location.ToString());
        return match.Success
            ? Guid.Parse(match.Value)
            : throw new InvalidOperationException($"Tidak ada Guid di Location: {location}");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<value>[^\"]+)\"")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();
}
