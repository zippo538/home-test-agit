using System.Security.Claims;
using home_test_agit.Services;

namespace home_test_agit.Auth;

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
            var raw = _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
