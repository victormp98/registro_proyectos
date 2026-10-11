using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;
using PortalProyectos.Web.Services;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Administracion")]
public sealed class UsersController(
    IAccountAdministrationService accountService,
    UserManager<ApplicationUser> userManager,
    PortalDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        AccountStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        ViewBag.Status = status;
        ViewBag.Search = search;
        return View(await accountService.GetStudentsAsync(status, search, cancellationToken));
    }

    [HttpPost]
    public Task<IActionResult> Approve(string id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, AccountStatus.Approved, null, cancellationToken);

    [HttpPost]
    public Task<IActionResult> Reject(string id, string? reason, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, AccountStatus.Rejected, reason, cancellationToken);

    [HttpPost]
    public Task<IActionResult> Block(string id, string? reason, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, AccountStatus.Blocked, reason, cancellationToken);

    [HttpPost]
    public Task<IActionResult> Delete(string id, string? reason, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, AccountStatus.Deactivated, reason, cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Unblock(string id, CancellationToken cancellationToken)
    {
        var (administratorId, administratorName) = await GetAdministratorAsync();
        var result = await accountService.UnblockAsync(
            id,
            administratorId,
            administratorName,
            cancellationToken);
        SetFlash(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var student = await userManager.FindByIdAsync(id);
        if (student is null || !await userManager.IsInRoleAsync(student, SystemRoles.Student)) return NotFound();
        if (student.AccountStatus != AccountStatus.Approved)
        {
            TempData["Error"] = "Solo se puede restablecer desde esta pantalla la contraseña de una cuenta aprobada.";
            return RedirectToAction(nameof(Index));
        }

        var reset = await TemporaryPasswordReset.ApplyAsync(userManager, student);
        if (reset.Password is null)
        {
            TempData["Error"] = "No se pudo restablecer la contraseña. No se aplicó el cambio.";
            return RedirectToAction(nameof(Index));
        }
        var actor = await userManager.GetUserAsync(User);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor?.Id,
            UserDisplayName = actor?.FullName ?? "Administración",
            Action = "Alumno.ContrasenaRestablecida",
            EntityType = "Usuario",
            EntityId = student.Id,
            Summary = "Contraseña restablecida con credencial temporal de un solo uso."
        });
        await db.SaveChangesAsync();
        TempData["OneTimeCredential"] = $"Contraseña temporal para {student.EnrollmentNumber} (se muestra una sola vez): {reset.Password}";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ChangeStatusAsync(
        string id,
        AccountStatus status,
        string? reason,
        CancellationToken cancellationToken)
    {
        var (administratorId, administratorName) = await GetAdministratorAsync();
        var result = await accountService.ChangeStatusAsync(
            id,
            status,
            reason,
            administratorId,
            administratorName,
            cancellationToken);
        SetFlash(result);
        return RedirectToAction(nameof(Index));
    }

    private async Task<(string Id, string Name)> GetAdministratorAsync()
    {
        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("No se encontró al administrador autenticado.");
        return (user.Id, user.FullName);
    }

    private void SetFlash(OperationResult result) =>
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

}
