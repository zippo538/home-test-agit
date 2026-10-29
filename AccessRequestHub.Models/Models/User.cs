namespace home_test_agit.Models;

public class User
{
    public int Id { get; set; }

    /// <summary>UNIQUE.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Null jika user tidak punya atasan (Bob, Carol, Dana, Erin). Hanya user dengan manager yang boleh membuat request (D-11).</summary>
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }

    /// <summary>Admin/Auditor: boleh melihat semua request dan audit, tetapi bukan approver otomatis.</summary>
    public bool IsAuditor { get; set; }

    public ICollection<User> DirectReports { get; set; } = new List<User>();
    public ICollection<Application> OwnedApplications { get; set; } = new List<Application>();
}
