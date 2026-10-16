namespace home_test_agit.Models;

public class PolicyVersion
{
    public int Id { get; set; }

    /// <summary>UNIQUE. Phase 1: "v1".</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
