using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Users;

namespace PortalProyectos.Web.Models.Student;

public sealed class StudentProjectsViewModel
{
    public AccountStatus AccountStatus { get; set; }
    public ProjectAdministrativeStatus? Status { get; set; }
    public string Scope { get; set; } = "all";
    public IReadOnlyList<ProjectListItem> Projects { get; set; } = [];
}

public sealed class ProjectEditorViewModel
{
    public int Id { get; set; }
    public ProjectAdministrativeStatus Status { get; set; }
    public int VersionNumber { get; set; }

    [StringLength(120, ErrorMessage = "El nombre admite máximo 120 caracteres.")]
    [Display(Name = "Nombre del proyecto")]
    public string? Name { get; set; }

    [Display(Name = "Materia")]
    public int? SubjectId { get; set; }

    [Display(Name = "Tipo de proyecto")]
    public int? ProjectTypeId { get; set; }

    [Display(Name = "Ciclo escolar")]
    public int? SchoolCycleId { get; set; }

    [StringLength(3000, ErrorMessage = "El planteamiento admite máximo 3000 caracteres.")]
    [Display(Name = "Planteamiento del problema")]
    public string? ProblemStatement { get; set; }

    [StringLength(3000, ErrorMessage = "La solución admite máximo 3000 caracteres.")]
    [Display(Name = "Solución propuesta o innovación")]
    public string? ProposedSolution { get; set; }

    [StringLength(160)]
    [Display(Name = "Asesor")]
    public string? Advisor { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha estimada de terminación")]
    public DateOnly? EstimatedCompletionDate { get; set; }

    public IReadOnlyList<SelectListItem> Subjects { get; set; } = [];
    public IReadOnlyList<SelectListItem> ProjectTypes { get; set; } = [];
    public IReadOnlyList<SelectListItem> SchoolCycles { get; set; } = [];
    public IReadOnlyList<ProjectParticipantItem> Participants { get; set; } = [];
    public IReadOnlyList<ProjectReviewItem> Reviews { get; set; } = [];
    public int MaxParticipants { get; set; }
    public ProjectDocumentItem? Document { get; set; }
    public bool HasSavedBasics { get; set; }
}

public sealed record ProjectDocumentCardViewModel(int ProjectId, ProjectDocumentItem? Document, bool Editable, bool ApprovedPostReview = false);

public sealed record StudentDashboardViewModel(
    PortalProyectos.Application.Users.StudentProfile Profile,
    StudentProjectDashboardSummary Projects,
    IReadOnlyList<ProjectListItem> RecentProjects);

public sealed record ProjectParticipantsViewModel(int Id, IReadOnlyList<ProjectParticipantItem> Participants, int MaxParticipants);
