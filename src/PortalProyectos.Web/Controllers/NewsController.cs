using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.News;

namespace PortalProyectos.Web.Controllers;

public sealed class NewsController(INewsPostService newsService) : Controller
{
    [HttpGet("/noticias")]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Search = search;
        return View(await newsService.GetPublicPageAsync(search, page, cancellationToken));
    }

    [HttpGet("/noticias/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var post = await newsService.GetPublicDetailsAsync(id, cancellationToken);
        return post is null ? NotFound() : View(post);
    }
}
