using System.Security.Claims;

namespace ClaroFlowEngine.Api.Common.Services;

public class CurrentPanelUserAccessor : ICurrentPanelUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentPanelUserAccessor(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var sub = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? FullName => User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value;
}
