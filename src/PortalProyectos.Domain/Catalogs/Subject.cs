using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Catalogs;

public sealed class Subject
{
    public int Id { get; set; }

    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(24)]
    public string? Code { get; set; }

    public int CareerId { get; set; }
    public Career? Career { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
