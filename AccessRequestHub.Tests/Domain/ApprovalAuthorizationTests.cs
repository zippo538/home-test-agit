using AccessRequestHub.Models.Domain;
using AccessRequestHub.Models;
using AccessRequestHub.Models.Enums;

namespace AccessRequestHub.Tests.Domain;

/// <summary>
/// AC-12, AC-13, AC-18: keputusan siapa yang boleh approve/reject.
/// Diuji tanpa database supaya aturan otorisasi tidak bisa "lolos" karena efek samping.
/// </summary>
public class ApprovalAuthorizationTests
{
    private static User User(int id, string name, int? managerId = null, bool auditor = false)
        => new() { Id = id, Email = $"{name}@example.local", DisplayName = name, ManagerId = managerId, IsAuditor = auditor };

    private static Application App(int id, string name, int ownerId)
        => new() { Id = id, Name = name, SystemOwnerId = ownerId };

    /// <summary>Membentuk request dalam status tertentu dengan graph Requester/Application terisi.</summary>
    private static AccessRequest Request(
        RequestStatus status, User requester, Application application, bool highRisk = false)
    {
        var request = (AccessRequest)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(AccessRequest));

        Set(request, nameof(AccessRequest.Status), status);
        Set(request, nameof(AccessRequest.RequesterId), requester.Id);
        Set(request, nameof(AccessRequest.Requester), requester);
        Set(request, nameof(AccessRequest.ApplicationId), application.Id);
        Set(request, nameof(AccessRequest.Application), application);
        Set(request, nameof(AccessRequest.IsHighRisk), highRisk);
        return request;
    }

    private static void Set(object target, string property, object value)
        => target.GetType().GetProperty(property)!.SetValue(target, value);

    private static readonly User Alice = User(1, "alice", managerId: 2);
    private static readonly User Bob = User(2, "bob");
    private static readonly User Carol = User(3, "carol");
    private static readonly User Erin = User(5, "erin", auditor: true);
    private static readonly Application Crm = App(1, "CRM", ownerId: 3);

    // ------------------------------------------------------------ AC-12 / AC-13

    [Fact]
    public void Manager_direct_report_boleh_approve_tahap_manager()
    {
        var request = Request(RequestStatus.PendingManager, Alice, Crm);

        Assert.True(ApprovalAuthorization.CanAct(Bob, request));
    }

    [Fact]
    public void Requester_tidak_boleh_approve_requestnya_sendiri()
    {
        var request = Request(RequestStatus.PendingManager, Alice, Crm);

        Assert.True(ApprovalAuthorization.IsSelfApproval(Alice, request));
        Assert.False(ApprovalAuthorization.CanAct(Alice, request));
    }

    [Fact]
    public void User_tanpa_kaitan_tidak_boleh_approve()
    {
        var request = Request(RequestStatus.PendingManager, Alice, Crm);

        Assert.False(ApprovalAuthorization.CanAct(Carol, request)); // bukan manager Alice
    }

    [Fact]
    public void System_owner_boleh_approve_tahap_system_owner()
    {
        var request = Request(RequestStatus.PendingSystemOwner, Alice, Crm, highRisk: true);

        Assert.True(ApprovalAuthorization.CanAct(Carol, request));
    }

    [Fact]
    public void Manager_tidak_boleh_approve_saat_sudah_tahap_system_owner()
    {
        var request = Request(RequestStatus.PendingSystemOwner, Alice, Crm, highRisk: true);

        // AC-14: Bob sudah lewat gilirannya.
        Assert.False(ApprovalAuthorization.CanAct(Bob, request));
        Assert.False(ApprovalAuthorization.MatchesCurrentStage(Bob, request));
    }

    // ------------------------------------------------------------------- AC-18

    [Fact]
    public void Auditor_bukan_approver()
    {
        var pendingManager = Request(RequestStatus.PendingManager, Alice, Crm);
        var pendingOwner = Request(RequestStatus.PendingSystemOwner, Alice, Crm, highRisk: true);

        Assert.True(Erin.IsAuditor);
        Assert.False(ApprovalAuthorization.CanAct(Erin, pendingManager));
        Assert.False(ApprovalAuthorization.CanAct(Erin, pendingOwner));
    }

    [Fact]
    public void Auditor_boleh_melihat_semua()
    {
        var request = Request(RequestStatus.PendingManager, Alice, Crm);

        Assert.True(ApprovalAuthorization.CanView(Erin, request));
    }

    // ------------------------------------------------------------------- AC-09

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    public void Status_terminal_tidak_bisa_diproses(RequestStatus terminal)
    {
        var request = Request(terminal, Alice, Crm);

        Assert.False(ApprovalAuthorization.CanAct(Bob, request));
        Assert.False(ApprovalAuthorization.CanAct(Carol, request));
    }

    // ------------------------------------------------------------------- AC-19

    [Fact]
    public void Pemilik_manager_dan_system_owner_boleh_melihat()
    {
        var request = Request(RequestStatus.PendingManager, Alice, Crm);

        Assert.True(ApprovalAuthorization.CanView(Alice, request));
        Assert.True(ApprovalAuthorization.CanView(Bob, request));
        Assert.True(ApprovalAuthorization.CanView(Carol, request));
        Assert.False(ApprovalAuthorization.CanView(User(4, "dana"), request)); // tidak terkait
    }
}
