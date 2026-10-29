using AccessRequestHub.Models;
using AccessRequestHub.Models.Enums;

namespace AccessRequestHub.Web.ViewModels;

public class CreateRequestViewModel
{
    /// <summary>
    /// Guid dibuat saat form dirender dan dikirim balik apa adanya, sehingga retry
    /// (double click / refresh) tidak menghasilkan request kedua (D-05).
    /// </summary>
    public Guid ClientRequestId { get; set; } = Guid.NewGuid();

    public int ApplicationId { get; set; }

    public TargetEnvironment Environment { get; set; }

    public AccessLevel AccessLevel { get; set; }

    public string? Justification { get; set; }

    public List<Application> Applications { get; set; } = new();

    /// <summary>False untuk user tanpa manager (D-11): form tidak ditampilkan.</summary>
    public bool CanCreate { get; set; } = true;

    public string? Notice { get; set; }
}

public class RequestDetailViewModel
{
    public AccessRequest Request { get; set; } = null!;

    /// <summary>True hanya bila user berhak memproses tahap yang sedang aktif (D-14).</summary>
    public bool CanAct { get; set; }

    public string? ConflictMessage { get; set; }

    public string? ConflictCode { get; set; }
}

public class SwitchUserViewModel
{
    public List<User> Users { get; set; } = new();

    public string? ReturnUrl { get; set; }
}
