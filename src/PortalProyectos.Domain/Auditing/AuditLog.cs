using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Auditing;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(160)]
    public required string UserDisplayName { get; set; }

    [MaxLength(80)]
    public required string Action { get; set; }

    [MaxLength(80)]
    public required string EntityType { get; set; }

    [MaxLength(80)]
    public required string EntityId { get; set; }

    [MaxLength(800)]
    public string? Summary { get; set; }
}
