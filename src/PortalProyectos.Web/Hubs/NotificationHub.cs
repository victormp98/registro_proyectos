using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PortalProyectos.Domain.Security;

namespace PortalProyectos.Web.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public const string AdministratorsGroup = "Admins";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(SystemRoles.Administrator) == true
            || Context.User?.IsInRole(SystemRoles.SuperAdministrator) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdministratorsGroup);
        await base.OnConnectedAsync();
    }
}
