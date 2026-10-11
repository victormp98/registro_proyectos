namespace PortalProyectos.Domain.Projects;

public sealed class Project
{
    public int Id { get; set; }
    public required string ResponsibleUserId { get; set; }
    public ProjectAdministrativeStatus CurrentStatus { get; set; } = ProjectAdministrativeStatus.Draft;
    public int? CurrentVersionId { get; set; }
    public int? LatestApprovedVersionId { get; set; }
    public string? AssignedAdministratorId { get; set; }
    public DateTimeOffset? ReviewStartedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<ProjectVersion> Versions { get; set; } = [];
    public ICollection<ProjectParticipant> Participants { get; set; } = [];
    public ICollection<ProjectReview> Reviews { get; set; } = [];
    public ICollection<ProjectHistoryEvent> History { get; set; } = [];
    public ICollection<ProjectLike> ProjectLikes { get; set; } = [];
}
