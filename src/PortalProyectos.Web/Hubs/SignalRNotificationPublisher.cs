using Microsoft.AspNetCore.SignalR;
using PortalProyectos.Application.Notifications;

namespace PortalProyectos.Web.Hubs;

public sealed class SignalRNotificationPublisher(IHubContext<NotificationHub> hub) : INotificationPublisher
{
    public Task PublishAsync(string userId, NotificationItem notification, CancellationToken cancellationToken = default) =>
        hub.Clients.User(userId).SendAsync("ReceiveNotification", notification, cancellationToken);

    public Task PublishUnreadCountAsync(string userId, int unreadCount, CancellationToken cancellationToken = default) =>
        hub.Clients.User(userId).SendAsync("UnreadCountChanged", unreadCount, cancellationToken);
}
