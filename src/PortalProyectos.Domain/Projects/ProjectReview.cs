using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Projects;

public sealed class ProjectReview
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int ProjectVersionId { get; set; }
    public ProjectVersion? ProjectVersion { get; set; }
    public int ReviewNumber { get; set; }
    public string? AdministratorId { get; set; }
    public ProjectReviewDecision Decision { get; set; } = ProjectReviewDecision.Pending;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public ICollection<ProjectReviewRequirement> Requirements { get; set; } = [];
}
