using AccessRequestHub.Models.Enums;

namespace AccessRequestHub.Models.Domain;

/// <summary>
/// Keputusan otorisasi approval (SPECS 3.3, D-13/D-14). Satu tempat, dipakai oleh
/// service (penegakan) dan controller (menentukan tombol mana yang ditampilkan).
/// Menampilkan tombol bukan pengaman: penegakan tetap terjadi di service (AC-10).
/// </summary>
public static class ApprovalAuthorization
{
    public static bool IsRequester(User actor, AccessRequest request)
        => request.RequesterId == actor.Id;

    public static bool IsManagerOfRequester(User actor, AccessRequest request)
        => request.Requester.ManagerId == actor.Id;

    public static bool IsSystemOwnerOfApplication(User actor, AccessRequest request)
        => request.Application.SystemOwnerId == actor.Id;

    /// <summary>Requester sendiri tidak boleh memproses requestnya (AC-12).</summary>
    public static bool IsSelfApproval(User actor, AccessRequest request)
        => IsRequester(actor, request);

    /// <summary>Apakah actor adalah approver yang ditunggu pada tahap saat ini (D-14).</summary>
    public static bool MatchesCurrentStage(User actor, AccessRequest request)
        => WorkflowRules.ExpectedApprover(request.Status) switch
        {
            ApproverRole.Manager => IsManagerOfRequester(actor, request),
            ApproverRole.SystemOwner => IsSystemOwnerOfApplication(actor, request),
            _ => false
        };

    /// <summary>True bila actor boleh melihat request ini (D-15).</summary>
    public static bool CanView(User actor, AccessRequest request)
        => actor.IsAuditor
           || IsRequester(actor, request)
           || IsManagerOfRequester(actor, request)
           || IsSystemOwnerOfApplication(actor, request);

    /// <summary>
    /// True bila actor boleh approve/reject sekarang.
    /// Auditor bukan approver (AC-18), dan status terminal tidak bisa diproses (AC-09).
    /// </summary>
    public static bool CanAct(User actor, AccessRequest request)
        => !request.Status.IsTerminal()
           && !IsSelfApproval(actor, request)
           && MatchesCurrentStage(actor, request);
}
