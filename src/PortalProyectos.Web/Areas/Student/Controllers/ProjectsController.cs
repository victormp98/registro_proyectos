using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Security;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Web.Models.Student;

namespace PortalProyectos.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = SystemRoles.Student)]
public sealed class ProjectsController(
    IStudentProjectService projectService,
    IProjectCatalogService catalogService,
    IProjectDocumentService documentService,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(ProjectAdministrativeStatus? status, string? scope, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var selectedScope = scope switch
        {
            "linked" => "linked",
            "own" => "own",
            _ => "all"
        };
        var ownProjects = selectedScope == "linked"
            ? Array.Empty<ProjectListItem>()
            : await projectService.GetProjectsAsync(user.Id, status, cancellationToken);
        var linkedProjects = selectedScope == "all" || selectedScope == "linked"
            ? await projectService.GetLinkedProjectsAsync(user.Id, status, cancellationToken)
            : Array.Empty<ProjectListItem>();
        return View(new StudentProjectsViewModel
        {
            AccountStatus = user.AccountStatus,
            Status = status,
            Scope = selectedScope,
            Projects = ownProjects.Concat(linkedProjects).DistinctBy(x => x.Id).OrderByDescending(x => x.UpdatedAtUtc).ToArray()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var result = await projectService.CreateDraftAsync(user.Id, user.FullName, cancellationToken);
        if (!result.Succeeded)
        {
            SetFlash(result.Message, false);
            return RedirectToAction(nameof(Index));
        }

        SetFlash(result.Message, true);
        return RedirectToAction(nameof(Edit), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var details = await projectService.GetProjectAsync(GetUserId(), id, cancellationToken);
        if (details is null) return Forbid();
        if (details.Status is not (ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested))
            return RedirectToAction(nameof(Details), new { id });
        return View(await BuildEditorAsync(details, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Save(ProjectEditorViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var details = await projectService.GetProjectAsync(GetUserId(), model.Id, cancellationToken);
            if (details is not null && details.Status is not (ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested))
                return RedirectToAction(nameof(Details), new { id = model.Id });
            return details is null ? Forbid() : View("Edit", await BuildEditorAsync(details, cancellationToken, model));
        }

        var result = await SaveModelAsync(model, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    [HttpPost]
    public async Task<IActionResult> SaveAndSubmit(ProjectEditorViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var details = await projectService.GetProjectAsync(GetUserId(), model.Id, cancellationToken);
            if (details is not null && details.Status is not (ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested))
                return RedirectToAction(nameof(Details), new { id = model.Id });
            return details is null ? Forbid() : View("Edit", await BuildEditorAsync(details, cancellationToken, model));
        }

        var save = await SaveModelAsync(model, cancellationToken);
        if (!save.Succeeded)
        {
            SetFlash(save.Message, false);
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        var user = await GetUserAsync();
        var submit = await projectService.SubmitAsync(user.Id, user.FullName, model.Id, cancellationToken);
        SetFlash(submit.Message, submit.Succeeded);
        return RedirectToAction(submit.Succeeded ? nameof(Details) : nameof(Edit), new { id = model.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var details = await projectService.GetProjectAsync(userId, id, cancellationToken)
            ?? await projectService.GetLinkedProjectAsync(userId, id, cancellationToken);
        if (details is null) return Forbid();
        ViewBag.ProjectDocument = details.IsResponsible
            ? await documentService.GetForProjectOwnerAsync(userId, id, cancellationToken)
            : await documentService.GetForLinkedParticipantAsync(userId, id, cancellationToken);
        ViewBag.MaxParticipants = projectService.Limits.MaxParticipants;
        return View(details);
    }

    [HttpPost]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        var project = await projectService.GetProjectAsync(GetUserId(), id, cancellationToken);
        if (project is null) return Forbid();
        var destination = project.Status == ProjectAdministrativeStatus.Approved ? nameof(Details) : nameof(Edit);
        if (file is null) { SetFlash("Selecciona un archivo PDF.", false); return RedirectToAction(destination, new { id }); }
        var user = await GetUserAsync();
        await using var stream = file.OpenReadStream();
        var result = await documentService.UploadAsync(user.Id, user.FullName, id, file.FileName, file.ContentType, file.Length, stream, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(destination, new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDocument(int id, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var result = await documentService.DeleteAsync(user.Id, user.FullName, id, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Document(int id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var opened = await documentService.OpenForProjectOwnerAsync(userId, id, cancellationToken)
            ?? await documentService.OpenForLinkedParticipantAsync(userId, id, cancellationToken);
        return opened is null ? NotFound() : File(opened.Value.Content, opened.Value.Document.MimeType, opened.Value.Document.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var result = await projectService.SubmitAsync(user.Id, user.FullName, id, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(result.Succeeded ? nameof(Details) : nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> CancelSubmission(int id, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var result = await projectService.CancelSubmissionAsync(user.Id, user.FullName, id, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(result.Succeeded ? nameof(Edit) : nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var result = await projectService.DeleteDraftAsync(user.Id, user.FullName, id, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> FindParticipant(int projectId, string studentNumber, CancellationToken cancellationToken)
    {
        var result = await projectService.FindStudentAsync(GetUserId(), projectId, studentNumber ?? string.Empty, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Json(new { ok = true, student = result.Value })
            : BadRequest(new { ok = false, message = result.Message });
    }

    [HttpPost]
    public async Task<IActionResult> AddLinkedParticipant(int projectId, string linkedUserId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var result = await projectService.AddLinkedParticipantAsync(user.Id, user.FullName, projectId, linkedUserId, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        var project = await projectService.GetProjectAsync(user.Id, projectId, cancellationToken);
        return RedirectToAction(project?.Status == ProjectAdministrativeStatus.Approved ? nameof(Details) : nameof(Edit), new { id = projectId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveParticipant(int projectId, int participantId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync();
        var result = await projectService.RemoveParticipantAsync(user.Id, user.FullName, projectId, participantId, cancellationToken);
        SetFlash(result.Message, result.Succeeded);
        var project = await projectService.GetProjectAsync(user.Id, projectId, cancellationToken);
        return RedirectToAction(project?.Status == ProjectAdministrativeStatus.Approved ? nameof(Details) : nameof(Edit), new { id = projectId });
    }

    private async Task<OperationResult> SaveModelAsync(ProjectEditorViewModel model, CancellationToken cancellationToken) =>
        await projectService.UpdateDraftAsync(GetUserId(), model.Id,
            new ProjectDraftData(model.Name, model.SubjectId, model.ProjectTypeId, model.SchoolCycleId,
                ProjectDevelopmentStatus.InProgress, model.ProblemStatement, model.ProposedSolution, model.Advisor, model.EstimatedCompletionDate), cancellationToken);

    private async Task<ProjectEditorViewModel> BuildEditorAsync(ProjectDetails details, CancellationToken cancellationToken, ProjectEditorViewModel? posted = null)
    {
        var user = await GetUserAsync();
        var catalogs = await catalogService.GetProjectFormCatalogsAsync(user.CareerId ?? 0, cancellationToken);
        var model = posted ?? new ProjectEditorViewModel
        {
            Id = details.Id,
            Status = details.Status,
            VersionNumber = details.VersionNumber,
            Name = details.Name,
            SubjectId = details.SubjectId,
            ProjectTypeId = details.ProjectTypeId,
            SchoolCycleId = details.SchoolCycleId,
            ProblemStatement = details.ProblemStatement,
            ProposedSolution = details.ProposedSolution,
            Advisor = details.Advisor,
            EstimatedCompletionDate = details.EstimatedCompletionDate
        };
        model.Status = details.Status;
        model.VersionNumber = details.VersionNumber;
        model.Participants = details.Participants;
        model.Reviews = details.Reviews;
        model.Document = await documentService.GetForProjectOwnerAsync(user.Id, details.Id, cancellationToken);
        model.HasSavedBasics = !string.IsNullOrWhiteSpace(details.Name)
            && details.SubjectId.HasValue && details.ProjectTypeId.HasValue && details.SchoolCycleId.HasValue;
        model.MaxParticipants = projectService.Limits.MaxParticipants;
        model.Subjects = catalogs.Subjects.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        model.ProjectTypes = catalogs.ProjectTypes.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        model.SchoolCycles = catalogs.SchoolCycles.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        return model;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No se encontró el usuario autenticado.");

    private async Task<ApplicationUser> GetUserAsync() =>
        await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("No se encontró el usuario autenticado.");

    private void SetFlash(string message, bool success) => TempData[success ? "Success" : "Error"] = message;
}
