using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Domain.Security;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class NotificationService(
    PortalDbContext dbContext,
    INotificationPublisher publisher,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task<IReadOnlyList<long>> CreateForUserAsync(string userId, NotificationDraft draft, CancellationToken cancellationToken = default) =>
        PersistAsync([(userId, draft)], cancellationToken);

    public async Task<IReadOnlyList<long>> CreateForAdministratorsAsync(NotificationDraft draft, CancellationToken cancellationToken = default)
    {
        var administratorRoleNames = new[] { SystemRoles.Administrator, SystemRoles.SuperAdministrator };
        var roleIds = await dbContext.Roles.AsNoTracking().Where(x => x.Name != null && administratorRoleNames.Contains(x.Name))
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
        var userIds = await dbContext.UserRoles.AsNoTracking().Where(x => roleIds.Contains(x.RoleId))
            .Select(x => x.UserId).Distinct().ToArrayAsync(cancellationToken);
        return await PersistAsync(userIds.Select(id => (id, draft)), cancellationToken);
    }

    public async Task PublishAsync(IEnumerable<long> notificationIds, CancellationToken cancellationToken = default)
    {
        var ids = notificationIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        var notifications = await dbContext.Notifications.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var item in notifications)
        {
            var payload = ToItem(item);
            try
            {
                await publisher.PublishAsync(item.UserId, payload, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "No fue posible entregar por SignalR la notificación {NotificationId}; permanece guardada para el usuario {UserId}.", item.Id, item.UserId);
            }
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

    public async Task<IReadOnlyList<NotificationUnreadUpdate>> ResolveAdministratorNotificationsAsync(
        NotificationType type, string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        var notifications = dbContext.Notifications.Where(x => x.Type == type && x.EntityType == entityType
            && x.EntityId == entityId && !x.IsRead);
        var affectedUsers = await notifications.Select(x => x.UserId).Distinct().ToArrayAsync(cancellationToken);
        if (affectedUsers.Length == 0) return [];

        var now = DateTimeOffset.UtcNow;
        await notifications.ExecuteUpdateAsync(update => update
            .SetProperty(x => x.IsRead, true)
            .SetProperty(x => x.ReadAtUtc, now), cancellationToken);
        var updates = new List<NotificationUnreadUpdate>(affectedUsers.Length);
        foreach (var userId in affectedUsers)
            updates.Add(new NotificationUnreadUpdate(userId, await GetUnreadCountAsync(userId, cancellationToken)));
        return updates;
    }

    public async Task<IReadOnlyList<NotificationItem>> GetRecentAsync(string userId, int count = 8, CancellationToken cancellationToken = default) =>
        await dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId && !x.IsRead)
            .OrderByDescending(x => x.Id).Take(Math.Clamp(count, 1, 20))
            .Select(x => new NotificationItem(x.Id, x.Type, x.Title, x.Message, x.EntityType, x.EntityId, x.Url, x.IsRead, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public Task<NotificationItem?> GetByIdAsync(string userId, long notificationId, CancellationToken cancellationToken = default) =>
        dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId && x.Id == notificationId)
            .Select(x => new NotificationItem(x.Id, x.Type, x.Title, x.Message, x.EntityType, x.EntityId, x.Url, x.IsRead, x.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<NotificationPage> GetPageAsync(string userId, bool unreadOnly, int page, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new NotificationItem(x.Id, x.Type, x.Title, x.Message, x.EntityType, x.EntityId, x.Url, x.IsRead, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new NotificationPage(items, await GetUnreadCountAsync(userId, cancellationToken), page, pageSize, count);
    }

    public async Task<bool> MarkAsReadAsync(string userId, long notificationId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = await dbContext.Notifications.Where(x => x.Id == notificationId && x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAtUtc, now), cancellationToken);
        return changed == 1 || await dbContext.Notifications.AnyAsync(x => x.Id == notificationId && x.UserId == userId && x.IsRead, cancellationToken);
    }

    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAtUtc, now), cancellationToken);
    }

    private async Task<IReadOnlyList<long>> PersistAsync(IEnumerable<(string UserId, NotificationDraft Draft)> entries, CancellationToken cancellationToken)
    {
        var allCandidates = entries.Select(x => (x.UserId, Draft: Sanitize(x.Draft))).ToArray();
        var candidates = allCandidates.Where(x => x.Draft.DeduplicationKey is null)
            .Concat(allCandidates.Where(x => x.Draft.DeduplicationKey is not null)
                .DistinctBy(x => (x.UserId, x.Draft.DeduplicationKey)))
            .ToArray();
        var keys = candidates.Where(x => x.Draft.DeduplicationKey is not null)
            .Select(x => x.Draft.DeduplicationKey + ":" + x.UserId).ToArray();
        var existing = keys.Length == 0 ? [] : await dbContext.Notifications.AsNoTracking()
            .Where(x => x.DeduplicationKey != null && keys.Contains(x.DeduplicationKey))
            .Select(x => x.DeduplicationKey!).ToArrayAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        var additions = candidates.Where(x => x.Draft.DeduplicationKey is null || !existingSet.Contains(x.Draft.DeduplicationKey + ":" + x.UserId))
            .Select(x => new Notification
            {
                UserId = x.UserId,
                Type = x.Draft.Type,
                Title = x.Draft.Title,
                Message = x.Draft.Message,
                EntityType = x.Draft.EntityType,
                EntityId = x.Draft.EntityId,
                Url = SafeLocalUrl(x.Draft.Url),
                DeduplicationKey = x.Draft.DeduplicationKey is null ? null : x.Draft.DeduplicationKey + ":" + x.UserId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            }).ToArray();
        if (additions.Length == 0) return [];
        dbContext.Notifications.AddRange(additions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return additions.Select(x => x.Id).ToArray();
    }

    private static NotificationDraft Sanitize(NotificationDraft draft) => draft with
    {
        Title = Clean(draft.Title, 120),
        Message = Clean(draft.Message, 500),
        EntityType = string.IsNullOrWhiteSpace(draft.EntityType) ? null : Clean(draft.EntityType, 40),
        EntityId = string.IsNullOrWhiteSpace(draft.EntityId) ? null : Clean(draft.EntityId, 80),
        DeduplicationKey = string.IsNullOrWhiteSpace(draft.DeduplicationKey) ? null : Clean(draft.DeduplicationKey, 200)
    };

    private static string Clean(string value, int maxLength) =>
        new string(value.Where(c => !char.IsControl(c)).Take(maxLength).ToArray()).Trim();

    private static string? SafeLocalUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\') ? url[..Math.Min(url.Length, 500)] : null;

    private static NotificationItem ToItem(Notification x) =>
        new(x.Id, x.Type, x.Title, x.Message, x.EntityType, x.EntityId, x.Url, x.IsRead, x.CreatedAtUtc);
}
