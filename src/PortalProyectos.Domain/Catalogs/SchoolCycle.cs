using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Catalogs;

public sealed class SchoolCycle
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
