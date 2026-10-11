using System.ComponentModel.DataAnnotations;
using PortalProyectos.Domain.Catalogs;

namespace PortalProyectos.Domain.Projects;

public sealed class ProjectVersion
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int VersionNumber { get; set; }

    [MaxLength(120)]
    public required string Name { get; set; }

    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int? ProjectTypeId { get; set; }
    public ProjectType? ProjectType { get; set; }
    public int? SchoolCycleId { get; set; }
    public SchoolCycle? SchoolCycle { get; set; }
    public ProjectDevelopmentStatus DevelopmentStatus { get; set; } = ProjectDevelopmentStatus.InProgress;

    [MaxLength(3000)]
    public required string ProblemStatement { get; set; }

    [MaxLength(3000)]
    public required string ProposedSolution { get; set; }

    [MaxLength(160)]
    public string? Advisor { get; set; }

    public DateOnly? EstimatedCompletionDate { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
}
