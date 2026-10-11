using PortalProyectos.Domain.Notifications;

namespace PortalProyectos.Application.Notifications;

public sealed record NotificationDraft(
    NotificationType Type,
    string Title,
    string Message,
    string? EntityType,
    string? EntityId,
    string? Url,
    string? DeduplicationKey);

public sealed record NotificationItem(
    long Id,
    NotificationType Type,
    string Title,
    string Message,
    string? EntityType,
    string? EntityId,
    string? Url,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationPage(IReadOnlyList<NotificationItem> Items, int UnreadCount, int Page, int PageSize, int TotalCount);

public sealed record StoredFileDescriptor(string StoredFileName, string RelativePath, long SizeBytes);

public sealed record ProjectDocumentItem(long Id, string OriginalFileName, string MimeType, long SizeBytes, DateTimeOffset CreatedAtUtc);
