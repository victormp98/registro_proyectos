using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Application.Catalogs;

namespace PortalProyectos.Web.Models.Admin;

public sealed class ProjectCatalogsViewModel
{
    public IReadOnlyList<SubjectItem> Subjects { get; set; } = [];
    public IReadOnlyList<SchoolCycleItem> SchoolCycles { get; set; } = [];
    public IReadOnlyList<ProjectTypeItem> ProjectTypes { get; set; } = [];
    public IReadOnlyList<ReviewRequirementItem> Requirements { get; set; } = [];
    public IReadOnlyList<AcademicLevelItem> Grades { get; set; } = [];
    public IReadOnlyList<AcademicLevelItem> Groups { get; set; } = [];
    public IReadOnlyList<SelectListItem> Careers { get; set; } = [];
}

public sealed class SubjectCatalogForm
{
    public int Id { get; set; }
    [Required, StringLength(120, MinimumLength = 3)] public string Name { get; set; } = string.Empty;
    [StringLength(24), RegularExpression("^[A-Za-z0-9-]*$")] public string? Code { get; set; }
    [Range(1, int.MaxValue)] public int CareerId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SchoolCycleCatalogForm
{
    public int Id { get; set; }
    [Required, StringLength(100, MinimumLength = 3)] public string Name { get; set; } = string.Empty;
    [DataType(DataType.Date)] public DateOnly? StartDate { get; set; }
    [DataType(DataType.Date)] public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProjectTypeCatalogForm
{
    public int Id { get; set; }
    [Required, StringLength(100, MinimumLength = 3)] public string Name { get; set; } = string.Empty;
    [StringLength(300)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class RequirementCatalogForm
{
    public int Id { get; set; }
    [Required, StringLength(120, MinimumLength = 3)] public string Name { get; set; } = string.Empty;
    [StringLength(300)] public string? Description { get; set; }
    [Range(0, 999)] public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
