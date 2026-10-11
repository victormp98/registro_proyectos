using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Security;
using PortalProyectos.Application.Projects;
using PortalProyectos.Web.Models.Student;

namespace PortalProyectos.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = SystemRoles.Student)]
public sealed class DashboardController(
    IStudentAccountService accountService,
    IStudentProjectService projectService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Challenge();
        }

        var profile = await accountService.GetProfileAsync(userId, cancellationToken);
        if (profile is null) return NotFound();
        var projects = await projectService.GetProjectsAsync(userId, cancellationToken: cancellationToken);
        return View(new StudentDashboardViewModel(
            profile,
            await projectService.GetDashboardSummaryAsync(userId, cancellationToken),
            projects.Take(4).ToArray()));
    }
}
