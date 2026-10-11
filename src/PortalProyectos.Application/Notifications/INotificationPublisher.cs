namespace PortalProyectos.Application.Notifications;

public interface INotificationPublisher
{
    Task PublishAsync(string userId, NotificationItem notification, CancellationToken cancellationToken = default);
    Task PublishUnreadCountAsync(string userId, int unreadCount, CancellationToken cancellationToken = default);
}
