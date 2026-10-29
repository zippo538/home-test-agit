using System.Security.Claims;
using AccessRequestHub.Web.Services;

namespace AccessRequestHub.Web.Auth;

public interface ICurrentUser
{
    int? UserId { get; }
}

/// <summary>
/// Actor selalu diambil dari principal yang sudah diautentikasi, bukan dari payload (AC-10).
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? UserId
    {
        get
        {
            var principal = _accessor.HttpContext?.User;
            var raw = principal?.FindFirstValue("arh_user_id")
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
