using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BuilderCore.Web.Hubs;

[Authorize]
public sealed class JobCostHub : Hub
{
    public static string GroupName(int jobId) => $"job:{jobId}";

    public Task JoinJobGroup(int jobId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(jobId));

    public Task LeaveJobGroup(int jobId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(jobId));
}
