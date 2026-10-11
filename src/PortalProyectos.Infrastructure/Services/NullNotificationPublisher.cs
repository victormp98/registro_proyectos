using PortalProyectos.Application.Notifications;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class NullNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync(string userId, NotificationItem notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishUnreadCountAsync(string userId, int unreadCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
