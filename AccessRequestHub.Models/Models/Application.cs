namespace home_test_agit.Models;

public class Application
{
    public int Id { get; set; }

    /// <summary>UNIQUE. Contoh: CRM, Finance Portal.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Satu aplikasi memiliki tepat satu System Owner.</summary>
    public int SystemOwnerId { get; set; }
    public User SystemOwner { get; set; } = null!;
}
