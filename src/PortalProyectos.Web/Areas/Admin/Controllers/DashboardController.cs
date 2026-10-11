using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Users;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Administracion")]
public sealed class DashboardController(IAccountAdministrationService accountService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await accountService.GetDashboardSummaryAsync(cancellationToken));
}
