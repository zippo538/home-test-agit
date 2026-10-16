namespace home_test_agit.Models;

/// <summary>
/// Satu-satunya tempat aturan high-risk (policy v1).
/// High-risk = Production ATAU Admin.
/// </summary>
public static class RiskPolicy
{
    public static bool IsHighRisk(TargetEnvironment environment, AccessLevel accessLevel)
        => environment == TargetEnvironment.Production || accessLevel == AccessLevel.Admin;
}
