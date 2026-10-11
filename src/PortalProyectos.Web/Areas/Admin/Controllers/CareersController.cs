using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Domain.Security;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Web.Models.Admin;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SystemRoles.SuperAdministrator)]
public sealed class CareersController(
    ICareerService careerService,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await careerService.GetAllAsync(false, cancellationToken));

    [HttpGet]
    public IActionResult Create() => View(new CareerFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CareerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var admin = await GetAdministratorAsync();
        var result = await careerService.CreateAsync(
            model.Name,
            model.Code,
            admin.Id,
            admin.FullName,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var career = await careerService.GetByIdAsync(id, cancellationToken);
        return career is null
            ? NotFound()
            : View(new CareerFormViewModel
            {
                Id = career.Id,
                Name = career.Name,
                Code = career.Code,
                IsActive = career.IsActive
            });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(CareerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var admin = await GetAdministratorAsync();
        var result = await careerService.UpdateAsync(
            model.Id,
            model.Name,
            model.Code,
            model.IsActive,
            admin.Id,
            admin.FullName,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser> GetAdministratorAsync() =>
        await userManager.GetUserAsync(User)
        ?? throw new InvalidOperationException("No se encontró al administrador autenticado.");
}
