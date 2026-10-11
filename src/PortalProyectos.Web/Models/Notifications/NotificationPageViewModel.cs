using PortalProyectos.Application.Notifications;

namespace PortalProyectos.Web.Models.Notifications;

public sealed record NotificationPageViewModel(NotificationPage Page, bool UnreadOnly);

public sealed record NotificationDropdownItem(
    long Id,
    string Type,
    string Title,
    string Message,
    string? EntityType,
    string? EntityId,
    string? Url,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationRecentResponse(
    IReadOnlyList<NotificationDropdownItem> Items,
    int UnreadCount);
