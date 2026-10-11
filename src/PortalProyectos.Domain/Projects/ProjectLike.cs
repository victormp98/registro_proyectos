using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Projects;

public sealed class ProjectLike
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(450)] public required string UserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
