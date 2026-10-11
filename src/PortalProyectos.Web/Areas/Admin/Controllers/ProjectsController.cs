using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Web.Models.Admin;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Administracion")]
public sealed class ProjectsController(
    IProjectReviewService reviewService,
    IProjectDocumentService documentService,
    IProjectCatalogService projectCatalogService,
    ICareerService careerService,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(ProjectAdministrativeStatus? status, int? careerId, int? subjectId, int? schoolCycleId, string? search, CancellationToken cancellationToken) =>
        View(await BuildListAsync(false, new AdminProjectFilter(status, careerId, subjectId, schoolCycleId, search), cancellationToken));

    [HttpGet("/Admin/Reviews")]
    public async Task<IActionResult> ReviewQueue(ProjectAdministrativeStatus? status, int? careerId, int? subjectId, int? schoolCycleId, string? search, CancellationToken cancellationToken)
    {
        var filter = new AdminProjectFilter(status, careerId, subjectId, schoolCycleId, search);
        var model = await BuildListAsync(true, filter, cancellationToken);
        if (!status.HasValue)
        {
            model.Projects = model.Projects.Where(x => x.Status is ProjectAdministrativeStatus.Submitted or ProjectAdministrativeStatus.Resubmitted or ProjectAdministrativeStatus.UnderReview).ToArray();
        }
        return View("Index", model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var project = await reviewService.GetProjectAsync(id, cancellationToken);
        if (project is null || project.Status == ProjectAdministrativeStatus.Draft) return NotFound();
        var administrator = await GetAdministratorAsync();
        ViewBag.ProjectDocument = await documentService.GetForAdministratorAsync(administrator.Id, id, cancellationToken);
        return View(new AdminProjectDetailsViewModel
        {
            Project = project,
            Requirements = await reviewService.GetActiveRequirementsAsync(cancellationToken),
            CanTake = project.Status is ProjectAdministrativeStatus.Submitted or ProjectAdministrativeStatus.Resubmitted,
            CanDecide = project.Status == ProjectAdministrativeStatus.UnderReview && project.AssignedAdministratorId == administrator.Id
        });
    }

    [HttpGet]
    public async Task<IActionResult> Document(int id, CancellationToken cancellationToken)
    {
        var opened = await documentService.OpenForAdministratorAsync((await GetAdministratorAsync()).Id, id, cancellationToken);
        return opened is null ? NotFound() : File(opened.Value.Content, opened.Value.Document.MimeType, opened.Value.Document.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost]
    public async Task<IActionResult> TakeReview(int id, CancellationToken cancellationToken)
    {
        var admin = await GetAdministratorAsync();
        var result = await reviewService.TakeReviewAsync(id, admin.Id, admin.FullName, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id, string? notes, CancellationToken cancellationToken)
    {
        var admin = await GetAdministratorAsync();
        var result = await reviewService.ApproveAsync(id, admin.Id, admin.FullName, notes, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? notes, CancellationToken cancellationToken)
    {
        var admin = await GetAdministratorAsync();
        var result = await reviewService.RejectAsync(id, admin.Id, admin.FullName, notes, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Finish(int id, CancellationToken cancellationToken)
    {
        var result = await reviewService.FinishAsync(id, (await GetAdministratorAsync()).Id, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Reopen(int id, CancellationToken cancellationToken)
    {
        var result = await reviewService.ReopenAsync(id, (await GetAdministratorAsync()).Id, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RequestChanges(ProjectDecisionViewModel model, CancellationToken cancellationToken)
    {
        var admin = await GetAdministratorAsync();
        var result = await reviewService.RequestChangesAsync(model.ProjectId, admin.Id, admin.FullName,
            new ProjectDecisionData(model.Notes, model.MissingRequirementIds), cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Details), new { id = model.ProjectId });
    }

    private async Task<AdminProjectsViewModel> BuildListAsync(bool reviewQueue, AdminProjectFilter filter, CancellationToken cancellationToken)
    {
        var careers = await careerService.GetAllAsync(true, cancellationToken);
        var subjects = await projectCatalogService.GetSubjectsAsync(true, cancellationToken);
        var cycles = await projectCatalogService.GetSchoolCyclesAsync(true, cancellationToken);
        return new AdminProjectsViewModel
        {
            ReviewQueue = reviewQueue,
            Filter = filter,
            Projects = await reviewService.GetProjectsAsync(filter, cancellationToken),
            Careers = careers.Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == filter.CareerId)).ToArray(),
            Subjects = subjects.Select(x => new SelectListItem($"{x.Career} · {x.Name}", x.Id.ToString(), x.Id == filter.SubjectId)).ToArray(),
            SchoolCycles = cycles.Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == filter.SchoolCycleId)).ToArray()
        };
    }

    private async Task<ApplicationUser> GetAdministratorAsync() =>
        await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("No se encontró al administrador autenticado.");

    private void SetFlash(string message, bool success) => TempData[success ? "Success" : "Error"] = message;
}
