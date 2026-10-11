using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Domain.Security;
using PortalProyectos.Infrastructure.Identity;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Administracion")]
[Route("Admin/Publications")]
public sealed class PublicationsController(IProjectPublicationService service, UserManager<ApplicationUser> userManager, ICareerService careerService, IProjectCatalogService catalogService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? state, int? careerId, int? cycleId, string? search, CancellationToken cancellationToken)
    {
        ViewBag.State = state; ViewBag.CareerId = careerId; ViewBag.CycleId = cycleId; ViewBag.Search = search;
        ViewBag.Careers = await careerService.GetAllAsync(true, cancellationToken);
        ViewBag.Cycles = await catalogService.GetSchoolCyclesAsync(true, cancellationToken);
        return View(await service.GetAdminItemsAsync(new PublicationAdminFilter(state, careerId, cycleId, search), cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await service.GetAdminDetailsAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("images/{mediaId:long}")]
    public async Task<IActionResult> Image(long mediaId, CancellationToken cancellationToken)
    {
        var opened = await service.OpenAdminImageAsync((await ActorAsync()).Id, mediaId, cancellationToken);
        return opened is null ? NotFound() : File(opened.Value.Content, opened.Value.MimeType, enableRangeProcessing: true);
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, int versionId, bool featured, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await service.PublishAsync(id, versionId, actor.Id, actor.FullName, featured, cancellationToken);
        Flash(result.Message, result.Succeeded); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/visibility")]
    public async Task<IActionResult> Visibility(int id, bool visible, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await service.SetVisibilityAsync(id, actor.Id, actor.FullName, visible, cancellationToken);
        Flash(result.Message, result.Succeeded); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/featured")]
    public async Task<IActionResult> Featured(int id, bool featured, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await service.SetFeaturedAsync(id, actor.Id, actor.FullName, featured, cancellationToken);
        Flash(result.Message, result.Succeeded); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(int id, IFormFile? image, string? altText, CancellationToken cancellationToken)
    {
        if (image is null) { Flash("Selecciona una imagen.", false); return RedirectToAction(nameof(Details), new { id }); }
        var actor = await ActorAsync();
        var result = await service.UploadImageAsync(id, actor.Id, actor.FullName, image.FileName, image.ContentType, image.Length, image.OpenReadStream(), altText, cancellationToken);
        Flash(result.Message, result.Succeeded); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/images/{mediaId:long}/cover")]
    public async Task<IActionResult> SetCover(int id, long mediaId, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await service.SetCoverAsync(id, mediaId, actor.Id, actor.FullName, cancellationToken);
        Flash(result.Message, result.Succeeded); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/images/{mediaId:long}/order")]
    public async Task<IActionResult> Reorder(int id, long mediaId, int order, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await service.ReorderImageAsync(id, mediaId, order, actor.Id, actor.FullName, cancellationToken);
        Flash(result.Message, result.Succeeded); return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/images/{mediaId:long}/delete")]
    public async Task<IActionResult> DeleteImage(int id, long mediaId, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await service.DeleteImageAsync(id, mediaId, actor.Id, actor.FullName, cancellationToken);
        Flash(result.Message, result.Succeeded); return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ApplicationUser> ActorAsync() => await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("No se encontró el administrador autenticado.");
    private void Flash(string message, bool success) => TempData[success ? "Success" : "Error"] = message;
}
