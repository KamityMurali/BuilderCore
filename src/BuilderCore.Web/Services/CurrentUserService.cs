using System.Security.Claims;

namespace BuilderCore.Web.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string UserId => User?.FindFirstValue("sub")
        ?? User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? string.Empty;

    public string? Email => User?.FindFirstValue("email")
        ?? User?.FindFirstValue("preferred_username")
        ?? User?.FindFirstValue(ClaimTypes.Email);

    public string DisplayName =>
        User?.FindFirstValue("name")
        ?? User?.FindFirstValue(ClaimTypes.Name)
        ?? Email
        ?? "User";
}
