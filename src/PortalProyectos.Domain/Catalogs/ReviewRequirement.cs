using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Catalogs;

public sealed class ReviewRequirement
{
    public int Id { get; set; }

    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
