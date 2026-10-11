using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Notifications;

public sealed class Notification
{
    public long Id { get; set; }
    [MaxLength(450)] public required string UserId { get; set; }
    public NotificationType Type { get; set; }
    [MaxLength(120)] public required string Title { get; set; }
    [MaxLength(500)] public required string Message { get; set; }
    [MaxLength(40)] public string? EntityType { get; set; }
    [MaxLength(80)] public string? EntityId { get; set; }
    [MaxLength(500)] public string? Url { get; set; }
    [MaxLength(256)] public string? DeduplicationKey { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAtUtc { get; set; }
}
