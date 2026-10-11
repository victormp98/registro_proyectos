using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Security;
using PortalProyectos.Web.Models.Student;

namespace PortalProyectos.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = SystemRoles.Student)]
public sealed class ProfileController(
    IStudentAccountService accountService,
    IRegistrationCatalogService catalogService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await GetCurrentProfileAsync(cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var model = new StudentProfileViewModel
        {
            FullName = profile.FullName,
            EnrollmentNumber = profile.EnrollmentNumber,
            Email = profile.Email,
            CareerId = profile.CareerId,
            GradeId = profile.GradeId,
            AcademicGroupId = profile.AcademicGroupId,
            Status = profile.Status,
            StatusReason = profile.StatusReason
        };

        return View(await PopulateCatalogsAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Index(StudentProfileViewModel model, CancellationToken cancellationToken)
    {
        var existing = await GetCurrentProfileAsync(cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        model.EnrollmentNumber = existing.EnrollmentNumber;
        model.Status = existing.Status;
        model.StatusReason = existing.StatusReason;

        if (!ModelState.IsValid)
        {
            return View(await PopulateCatalogsAsync(model, cancellationToken));
        }

        var result = await accountService.UpdateProfileAsync(
            existing.Id,
            new UpdateStudentProfileRequest(
                model.FullName,
                model.Email,
                model.CareerId,
                model.GradeId,
                model.AcademicGroupId),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(await PopulateCatalogsAsync(model, cancellationToken));
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<StudentProfile?> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await accountService.GetProfileAsync(userId, cancellationToken);
    }

    private async Task<StudentProfileViewModel> PopulateCatalogsAsync(
        StudentProfileViewModel model,
        CancellationToken cancellationToken)
    {
        var catalogs = await catalogService.GetAsync(cancellationToken);
        model.Careers = catalogs.Careers.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        model.Grades = catalogs.Grades.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        model.Groups = catalogs.Groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        return model;
    }
}
