using home_test_agit.Models;

namespace AccessRequestHub.Tests.Domain;

/// <summary>SPECS 3.3: tabel transisi. Terminal tidak boleh punya transisi keluar (AC-09).</summary>
public class WorkflowRulesTests
{
    [Fact]
    public void Non_high_risk_manager_approve_langsung_approved()
    {
        var t = WorkflowRules.Resolve(RequestStatus.PendingManager, isHighRisk: false, approve: true);

        Assert.Equal(AuditAction.ManagerApproved, t.Action);
        Assert.Equal(RequestStatus.Approved, t.To);
    }

    [Fact]
    public void High_risk_manager_approve_lanjut_ke_system_owner()
    {
        var t = WorkflowRules.Resolve(RequestStatus.PendingManager, isHighRisk: true, approve: true);

        Assert.Equal(AuditAction.ManagerApproved, t.Action);
        Assert.Equal(RequestStatus.PendingSystemOwner, t.To);
    }

    [Fact]
    public void System_owner_approve_menjadi_approved()
    {
        var t = WorkflowRules.Resolve(RequestStatus.PendingSystemOwner, isHighRisk: true, approve: true);

        Assert.Equal(AuditAction.SystemOwnerApproved, t.Action);
        Assert.Equal(RequestStatus.Approved, t.To);
    }

    [Theory]
    [InlineData(RequestStatus.PendingManager)]
    [InlineData(RequestStatus.PendingSystemOwner)]
    public void Reject_selalu_menuju_rejected(RequestStatus from)
    {
        var t = WorkflowRules.Resolve(from, isHighRisk: true, approve: false);

        Assert.Equal(AuditAction.Rejected, t.Action);
        Assert.Equal(RequestStatus.Rejected, t.To);
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    public void Status_terminal_tidak_punya_transisi(RequestStatus terminal)
    {
        Assert.True(terminal.IsTerminal());
        Assert.Throws<InvalidOperationException>(
            () => WorkflowRules.Resolve(terminal, isHighRisk: true, approve: true));
    }

    [Theory]
    [InlineData(RequestStatus.PendingManager, ApproverRole.Manager)]
    [InlineData(RequestStatus.PendingSystemOwner, ApproverRole.SystemOwner)]
    public void ExpectedApprover_sesuai_tahap(RequestStatus status, ApproverRole expected)
        => Assert.Equal(expected, WorkflowRules.ExpectedApprover(status));

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    public void ExpectedApprover_null_pada_status_terminal(RequestStatus terminal)
        => Assert.Null(WorkflowRules.ExpectedApprover(terminal));
}
