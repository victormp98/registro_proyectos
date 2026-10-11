using System.ComponentModel.DataAnnotations;
using PortalProyectos.Domain.Catalogs;

namespace PortalProyectos.Domain.Projects;

public sealed class ProjectParticipant
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string? UserId { get; set; }

    [MaxLength(160)]
    public required string FullName { get; set; }

    [MaxLength(32)]
    public required string StudentNumber { get; set; }

    public int CareerId { get; set; }
    public Career? Career { get; set; }
    public int GradeId { get; set; }
    public Grade? Grade { get; set; }
    public int AcademicGroupId { get; set; }
    public AcademicGroup? AcademicGroup { get; set; }
    public bool IsResponsible { get; set; }
    public bool LinkConfirmed { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
