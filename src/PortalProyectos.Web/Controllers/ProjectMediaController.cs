using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Projects;

namespace PortalProyectos.Web.Controllers;

[Route("media/projects")]
public sealed class ProjectMediaController(IProjectPublicationService publicationService) : Controller
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Image(long id, CancellationToken cancellationToken)
    {
        var opened = await publicationService.OpenPublicImageAsync(id, cancellationToken);
        if (opened is null) return NotFound();
        Response.Headers.CacheControl = "no-cache";
        return File(opened.Value.Content, opened.Value.MimeType, enableRangeProcessing: true);
    }
}
