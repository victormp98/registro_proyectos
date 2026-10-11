using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Projects;
using PortalProyectos.Domain.Projects;

namespace PortalProyectos.Web.Models.Admin;

public sealed class AdminProjectsViewModel
{
    public bool ReviewQueue { get; set; }
    public AdminProjectFilter Filter { get; set; } = new(null, null, null, null, null);
    public IReadOnlyList<AdminProjectListItem> Projects { get; set; } = [];
    public IReadOnlyList<SelectListItem> Careers { get; set; } = [];
    public IReadOnlyList<SelectListItem> Subjects { get; set; } = [];
    public IReadOnlyList<SelectListItem> SchoolCycles { get; set; } = [];
}

public sealed class AdminProjectDetailsViewModel
{
    public required ProjectDetails Project { get; set; }
    public IReadOnlyList<ReviewRequirementItem> Requirements { get; set; } = [];
    public bool CanDecide { get; set; }
    public bool CanTake { get; set; }
}

public sealed class ProjectDecisionViewModel
{
    public int ProjectId { get; set; }
    public string? Notes { get; set; }
    public int[] MissingRequirementIds { get; set; } = [];
}
