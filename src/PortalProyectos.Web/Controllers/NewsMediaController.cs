using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.News;

namespace PortalProyectos.Web.Controllers;

[Route("media/news")]
public sealed class NewsMediaController(INewsPostService newsService) : Controller
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Open(long id, CancellationToken cancellationToken)
    {
        var opened = await newsService.OpenPublicMediaAsync(id, cancellationToken);
        if (opened is null) return NotFound();
        Response.Headers.CacheControl = "no-cache";
        return File(opened.Value.Content, opened.Value.MimeType, enableRangeProcessing: opened.Value.MimeType == "video/mp4");
    }
}
