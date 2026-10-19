using home_test_agit.Models;

namespace AccessRequestHub.Tests.Domain;

/// <summary>SPECS 3.3 + AC-01..AC-03: aturan high-risk adalah satu-satunya sumber routing.</summary>
public class RiskPolicyTests
{
    [Theory]
    // Tabel kebenaran lengkap: 4 kombinasi Environment x AccessLevel.
    [InlineData(TargetEnvironment.NonProduction, AccessLevel.Read, false)]
    [InlineData(TargetEnvironment.NonProduction, AccessLevel.Admin, true)]
    [InlineData(TargetEnvironment.Production, AccessLevel.Read, true)]
    [InlineData(TargetEnvironment.Production, AccessLevel.Admin, true)]
    public void IsHighRisk_mengikuti_production_atau_admin(
        TargetEnvironment environment, AccessLevel accessLevel, bool expected)
        => Assert.Equal(expected, RiskPolicy.IsHighRisk(environment, accessLevel));
}
