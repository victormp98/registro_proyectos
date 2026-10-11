using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.News;
using PortalProyectos.Domain.News;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using PortalProyectos.Web.Models.News;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Administracion")]
[Route("Admin/News")]
public sealed class NewsController(INewsPostService newsService, UserManager<ApplicationUser> userManager, IOptions<StorageOptions> storageOptions) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(NewsPostStatus? status, bool featured = false, string? search = null, CancellationToken cancellationToken = default)
    {
        ViewBag.Status = status; ViewBag.Featured = featured; ViewBag.Search = search;
        return View(await newsService.GetAdminItemsAsync(new NewsFilter(search, status, featured), cancellationToken));
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Edit", new NewsPostFormViewModel());

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var post = await newsService.GetAdminDetailsAsync(id, cancellationToken);
        ViewBag.MaxNewsVideoBytes = Math.Clamp(storageOptions.Value.MaxNewsVideoBytes, 1, 500 * 1024 * 1024);
        return post is null ? NotFound() : View(NewsPostFormViewModel.From(post));
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(NewsPostFormViewModel model, bool publishNow, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { ViewBag.MaxNewsVideoBytes = MaxVideoBytes; return View("Edit", model); }
        var actor = await ActorAsync();
        var saved = await newsService.SaveDraftAsync(model.Id, actor.Id, actor.FullName,
            new NewsPostInput(model.Title, model.Summary, model.Content, model.IsFeatured), cancellationToken);
        if (!saved.Succeeded) { ModelState.AddModelError(string.Empty, saved.Message); ViewBag.MaxNewsVideoBytes = MaxVideoBytes; return View("Edit", model); }
        if (publishNow)
        {
            var published = await newsService.PublishAsync(saved.Value!, actor.Id, actor.FullName, cancellationToken);
            TempData[published.Succeeded ? "Success" : "Error"] = published.Message;
        }
        else TempData["Success"] = "Borrador guardado. Puedes agregar medios y revisar la vista previa.";
        return RedirectToAction(nameof(Edit), new { id = saved.Value });
    }

    [HttpGet("{id:int}/preview")]
    public async Task<IActionResult> Preview(int id, CancellationToken cancellationToken)
    {
        var post = await newsService.GetAdminDetailsAsync(id, cancellationToken);
        ViewBag.AdminPreview = true;
        return post is null ? NotFound() : View("~/Views/News/Details.cshtml", post);
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.PublishAsync(id, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/visibility")]
    public async Task<IActionResult> Visibility(int id, bool visible, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.SetVisibilityAsync(id, actor.Id, actor.FullName, visible, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/featured")]
    public async Task<IActionResult> Featured(int id, bool featured, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.SetFeaturedAsync(id, actor.Id, actor.FullName, featured, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/delete-draft")]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.DeleteDraftAsync(id, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(int id, IFormFile? image, string? altText, bool cover, CancellationToken cancellationToken)
    {
        if (image is null) { TempData["Error"] = "Selecciona una imagen."; return RedirectToAction(nameof(Edit), new { id }); }
        var actor = await ActorAsync();
        await using var stream = image.OpenReadStream();
        var result = await newsService.UploadImageAsync(id, actor.Id, actor.FullName, image.FileName, image.ContentType, image.Length, stream, altText, cover, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/video")]
    [RequestSizeLimit(510 * 1024 * 1024)]
    public async Task<IActionResult> UploadVideo(int id, IFormFile? video, CancellationToken cancellationToken)
    {
        if (video is null) { TempData["Error"] = "Selecciona un archivo MP4."; return RedirectToAction(nameof(Edit), new { id }); }
        var actor = await ActorAsync(); await using var stream = video.OpenReadStream();
        var result = await newsService.UploadVideoAsync(id, actor.Id, actor.FullName, video.FileName, video.ContentType, video.Length, stream, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/media/{mediaId:long}/delete")]
    public async Task<IActionResult> DeleteMedia(int id, long mediaId, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.DeleteMediaAsync(id, mediaId, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/media/{mediaId:long}/cover")]
    public async Task<IActionResult> SetCover(int id, long mediaId, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.SetCoverAsync(id, mediaId, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/media/{mediaId:long}/order")]
    public async Task<IActionResult> Reorder(int id, long mediaId, int order, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.ReorderMediaAsync(id, mediaId, order, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/external-video")]
    public async Task<IActionResult> ExternalVideo(int id, string? url, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.SetExternalVideoAsync(id, url, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/links")]
    public async Task<IActionResult> AddLink(int id, string label, string url, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.AddExternalLinkAsync(id, label, url, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:int}/links/{linkId:int}/delete")]
    public async Task<IActionResult> DeleteLink(int id, int linkId, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var result = await newsService.DeleteExternalLinkAsync(id, linkId, actor.Id, actor.FullName, cancellationToken);
        Flash(result); return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet("{id:int}/media/{mediaId:long}")]
    public async Task<IActionResult> Media(int id, long mediaId, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(); var opened = await newsService.OpenAdminMediaAsync(id, mediaId, actor.Id, cancellationToken);
        return opened is null ? NotFound() : File(opened.Value.Content, opened.Value.MimeType, enableRangeProcessing: opened.Value.MimeType == "video/mp4");
    }

    private long MaxVideoBytes => Math.Clamp(storageOptions.Value.MaxNewsVideoBytes, 1, 500 * 1024 * 1024);
    private async Task<ApplicationUser> ActorAsync() => await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("No se encontró el usuario administrativo.");
    private void Flash(PortalProyectos.Application.Common.OperationResult result) => TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
}
