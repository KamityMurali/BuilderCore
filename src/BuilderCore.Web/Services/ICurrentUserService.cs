namespace BuilderCore.Web.Services;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string UserId { get; }
    string? Email { get; }
    string DisplayName { get; }
}
