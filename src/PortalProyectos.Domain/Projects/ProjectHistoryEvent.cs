using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Projects;

public sealed class ProjectHistoryEvent
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? ProjectVersionId { get; set; }
    public int? ProjectReviewId { get; set; }
    public ProjectHistoryEventType EventType { get; set; }
    public ProjectAdministrativeStatus? FromStatus { get; set; }
    public ProjectAdministrativeStatus? ToStatus { get; set; }

    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    [MaxLength(160)]
    public required string ActorDisplayName { get; set; }

    [MaxLength(800)]
    public string? Notes { get; set; }

    public bool IsVisibleToStudent { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
