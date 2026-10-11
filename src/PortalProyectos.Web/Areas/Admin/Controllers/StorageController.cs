using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Storage;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "SuperAdministracion")]
[Route("Admin/Storage")]
public sealed class StorageController(IStorageDiagnosticsService diagnostics) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try { return View(await diagnostics.ScanAsync(cancellationToken)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TempData["Error"] = "No se pudo recorrer el almacenamiento. Revisa la ruta y sus permisos.";
            return View("Unavailable");
        }
    }
}
