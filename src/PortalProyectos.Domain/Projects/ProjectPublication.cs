using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Projects;

public sealed class ProjectPublication
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int PublishedProjectVersionId { get; set; }
    [MaxLength(20000)] public string? ParticipantsSnapshot { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset? HiddenAtUtc { get; set; }
    [MaxLength(450)] public required string PublishedByUserId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
