using AccessRequestHub.Models.Enums;

namespace AccessRequestHub.Models;

public class PolicyVersion
{
    public int Id { get; set; }

    /// <summary>UNIQUE. Phase 1: "v1".</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>
    /// Aturan high-risk policy v1 (satu-satunya sumber routing).
    /// High-risk = Production ATAU Admin.
    /// </summary>
    public bool IsHighRisk(TargetEnvironment environment, AccessLevel accessLevel)
        => environment == TargetEnvironment.Production || accessLevel == AccessLevel.Admin;
}
