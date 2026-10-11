using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.News;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Web.Models;

namespace PortalProyectos.Web.Controllers;

public class HomeController(IProjectPublicationService publicationService, INewsPostService newsService, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.PublicProjects = (await publicationService.GetPublicPageAsync(new PublicProjectFilter(null, null, null, null, null, null, null), 1, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken)).Items;
        ViewBag.FeaturedNews = await newsService.GetFeaturedAsync(3, cancellationToken);
        return View("Landing");
    }

    [Route("privacidad")]
    public IActionResult Privacy()
    {
        return View("PrivacyInfo");
    }

    [HttpGet("/Home/StatusCode")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodePage(int statusCode)
    {
        if (statusCode is not (StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound or StatusCodes.Status500InternalServerError))
            statusCode = StatusCodes.Status404NotFound;
        Response.StatusCode = statusCode;
        return View("StatusCode", statusCode);
    }

    [HttpGet("/proyectos")]
    public async Task<IActionResult> Projects([FromQuery] string? search, int? careerId, int? subjectId, int? typeId, PortalProyectos.Domain.Projects.ProjectDevelopmentStatus? developmentStatus, int? cycleId, string? sort, int page = 1, CancellationToken cancellationToken = default)
    {
        var filter = new PublicProjectFilter(search, careerId, subjectId, typeId, developmentStatus, cycleId, sort);
        ViewBag.Filter = filter;
        ViewBag.CanLike = await CanLikeAsync();
        return View(await publicationService.GetPublicPageAsync(filter, page, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken));
    }

    [HttpGet("/proyectos/{id:int}")]
    public async Task<IActionResult> ProjectDetails(int id, CancellationToken cancellationToken)
    {
        var project = await publicationService.GetPublicDetailsAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        ViewBag.CanLike = await CanLikeAsync();
        return project is null ? NotFound() : View("ProjectDetails", project);
    }

    private async Task<bool> CanLikeAsync()
    {
        var user = await userManager.GetUserAsync(User);
        return user is { AccountStatus: AccountStatus.Approved } && await userManager.IsInRoleAsync(user, SystemRoles.Student);
    }

    [HttpPost("/proyectos/{id:int}/like")]
    public async Task<IActionResult> ToggleLike(int id, CancellationToken cancellationToken)
    {
        if (!User.Identity?.IsAuthenticated ?? true) return Unauthorized();
        var result = await publicationService.ToggleLikeAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, cancellationToken);
        if (!result.Succeeded) return StatusCode(StatusCodes.Status403Forbidden, new { message = result.Message });
        var details = await publicationService.GetPublicDetailsAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        return details is null ? NotFound() : Json(new { liked = result.Value, total = details.Project.LikeCount, message = result.Message });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
