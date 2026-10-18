namespace AccessRequestHub.IdentityServer.Models;

/// <summary>
/// Ditampilkan saat Production menolak subject Microsoft yang belum disetujui, atau saat
/// Development meminta operator mengonfigurasi mapping <c>Microsoft:ApprovedSubjects:{subject}</c>.
/// Halaman ini tidak melakukan aksi apa pun; tautan hanya dibuat lewat konfigurasi oleh operator.
/// </summary>
public sealed class ExternalSubjectViewModel
{
    public string Subject { get; set; } = string.Empty;
    public string? MicrosoftEmail { get; set; }
    public string? ReturnUrl { get; set; }
}
