using PortalProyectos.Domain.Notifications;

namespace PortalProyectos.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<long>> CreateForUserAsync(string userId, NotificationDraft draft, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<long>> CreateForAdministratorsAsync(NotificationDraft draft, CancellationToken cancellationToken = default);
    Task PublishAsync(IEnumerable<long> notificationIds, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationUnreadUpdate>> ResolveAdministratorNotificationsAsync(NotificationType type, string entityType, string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationItem>> GetRecentAsync(string userId, int count = 8, CancellationToken cancellationToken = default);
    Task<NotificationItem?> GetByIdAsync(string userId, long notificationId, CancellationToken cancellationToken = default);
    Task<NotificationPage> GetPageAsync(string userId, bool unreadOnly, int page, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(string userId, long notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed record NotificationUnreadUpdate(string UserId, int UnreadCount);
