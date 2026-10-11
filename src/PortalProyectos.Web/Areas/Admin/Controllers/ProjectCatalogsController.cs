using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Domain.Security;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Web.Models.Admin;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.SuperAdministrator)]
public sealed class ProjectCatalogsController(
    IProjectCatalogService catalogService,
    ICareerService careerService,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await BuildModelAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> SaveSubject(SubjectCatalogForm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return InvalidForm("Verifica los datos de la materia.");
        var actor = await GetActorAsync();
        var result = await catalogService.SaveSubjectAsync(model.Id, model.Name, model.Code, model.CareerId, model.IsActive, actor.Id, actor.FullName, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Index), new { tab = "subjects" });
    }

    [HttpPost]
    public async Task<IActionResult> SaveSchoolCycle(SchoolCycleCatalogForm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return InvalidForm("Verifica los datos del ciclo escolar.");
        var actor = await GetActorAsync();
        var result = await catalogService.SaveSchoolCycleAsync(model.Id, model.Name, model.StartDate, model.EndDate, model.IsActive, actor.Id, actor.FullName, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Index), new { tab = "cycles" });
    }

    [HttpPost]
    public async Task<IActionResult> SaveProjectType(ProjectTypeCatalogForm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return InvalidForm("Verifica los datos del tipo de proyecto.");
        var actor = await GetActorAsync();
        var result = await catalogService.SaveProjectTypeAsync(model.Id, model.Name, model.Description, model.IsActive, actor.Id, actor.FullName, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Index), new { tab = "types" });
    }

    [HttpPost]
    public async Task<IActionResult> SaveRequirement(RequirementCatalogForm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return InvalidForm("Verifica los datos del requisito.");
        var actor = await GetActorAsync();
        var result = await catalogService.SaveRequirementAsync(model.Id, model.Name, model.Description, model.SortOrder, model.IsActive, actor.Id, actor.FullName, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Index), new { tab = "requirements" });
    }

    [HttpPost]
    public async Task<IActionResult> SaveGrade(AcademicLevelCatalogForm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { SetFlash("Verifica los datos del grado.", false); return RedirectToAction(nameof(Index), new { tab = "grades" }); }
        var actor = await GetActorAsync();
        var result = await catalogService.SaveGradeAsync(model.Id, model.Name, model.SortOrder, model.IsActive, actor.Id, actor.FullName, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Index), new { tab = "grades" });
    }

    [HttpPost]
    public async Task<IActionResult> SaveGroup(AcademicLevelCatalogForm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { SetFlash("Verifica los datos del grupo.", false); return RedirectToAction(nameof(Index), new { tab = "groups" }); }
        var actor = await GetActorAsync();
        var result = await catalogService.SaveGroupAsync(model.Id, model.Name, model.SortOrder, model.IsActive, actor.Id, actor.FullName, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Index), new { tab = "groups" });
    }

    private async Task<ProjectCatalogsViewModel> BuildModelAsync(CancellationToken cancellationToken)
    {
        var careers = await careerService.GetAllAsync(true, cancellationToken);
        return new ProjectCatalogsViewModel
        {
            Subjects = await catalogService.GetSubjectsAsync(false, cancellationToken),
            SchoolCycles = await catalogService.GetSchoolCyclesAsync(false, cancellationToken),
            ProjectTypes = await catalogService.GetProjectTypesAsync(false, cancellationToken),
            Requirements = await catalogService.GetRequirementsAsync(false, cancellationToken),
            Grades = await catalogService.GetGradesAsync(cancellationToken),
            Groups = await catalogService.GetGroupsAsync(cancellationToken),
            Careers = careers.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray()
        };
    }

    private IActionResult InvalidForm(string message)
    {
        SetFlash(message, false);
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser> GetActorAsync() =>
        await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("No se encontró al usuario autenticado.");

    private void SetFlash(string message, bool success) => TempData[success ? "Success" : "Error"] = message;
}

public sealed class AcademicLevelCatalogForm
{
    public int Id { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(40, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Range(0, 999)] public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
