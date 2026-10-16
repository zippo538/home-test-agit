namespace home_test_agit.Models;

/// <summary>Dipakai oleh halaman error bawaan template (HomeController.Error).</summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
