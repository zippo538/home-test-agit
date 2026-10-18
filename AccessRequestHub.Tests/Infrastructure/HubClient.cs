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
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        http.DefaultRequestHeaders.Add("X-Demo-User", email);
        return new HubClient(http, email);
    }

    public Task<HttpResponseMessage> GetAsync(string url) => _http.GetAsync(url);

    public async Task<string> GetStringAsync(string url)
        => await (await _http.GetAsync(url)).Content.ReadAsStringAsync();

    public async Task<HttpResponseMessage> PostFormAsync(
        string url, IDictionary<string, string> fields, string tokenPageUrl)
    {
        var token = await FetchTokenAsync(tokenPageUrl);
        var payload = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = token
        };

        return await _http.PostAsync(url, new FormUrlEncodedContent(payload));
    }

    private async Task<string> FetchTokenAsync(string pageUrl)
    {
        var html = await GetStringAsync(pageUrl);
        var token = ExtractToken(html);
        if (token is not null)
            return token;

        // Some actors cannot act on Details or create requests, so their page has no form.
        html = await GetStringAsync("/Account/Login");
        token = ExtractToken(html);
        return token ?? throw new InvalidOperationException(
            $"Antiforgery token tidak ditemukan di {pageUrl} atau /Account/Switch. Response: {html[..Math.Min(html.Length, 400)]}");
    }

    private static string? ExtractToken(string html)
    {
        foreach (Match tag in InputTagRegex().Matches(html))
        {
            if (!tag.Value.Contains("__RequestVerificationToken", StringComparison.Ordinal))
                continue;

            var value = InputValueRegex().Match(tag.Value);
            if (value.Success)
                return value.Groups["value"].Value;
        }

        return null;
    }

    [GeneratedRegex("<input\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex("\\bvalue=[\\\"'](?<value>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase)]
    private static partial Regex InputValueRegex();

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

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();
}
