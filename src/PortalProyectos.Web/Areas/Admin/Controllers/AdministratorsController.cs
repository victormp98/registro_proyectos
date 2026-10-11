using PortalProyectos.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;
using PortalProyectos.Web.Models.Admin;
using PortalProyectos.Web.Services;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "SuperAdministracion")]
[Route("Admin/Administrators")]
public sealed class AdministratorsController(
    PortalDbContext db,
    UserManager<ApplicationUser> users,
    IAccountAdministrationService accounts) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, AccountStatus? status, CancellationToken cancellationToken)
    {
        ViewBag.Search = search;
        ViewBag.Status = status;
        var administrators = await users.GetUsersInRoleAsync(SystemRoles.Administrator);
        var term = search?.Trim();
        var items = administrators.Where(x => !status.HasValue || x.AccountStatus == status.Value).Where(x => string.IsNullOrWhiteSpace(term)
                || x.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.EnrollmentNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (x.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new AdministrativeUserItem(x.Id, x.FullName, x.EnrollmentNumber, x.Email ?? string.Empty, x.AccountStatus, x.CreatedAtUtc))
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return View(items);
    }

    [HttpGet("create")]
    public IActionResult Create() => View(new AdministrativeUserForm());

    [HttpPost("create")]
    public async Task<IActionResult> Create(AdministrativeUserForm model)
    {
        if (!ModelState.IsValid) return View(model);
        var employee = model.EmployeeNumber.Trim().ToUpperInvariant();
        var email = model.Email.Trim();
        var tempPassword = TemporaryPasswordReset.GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            FullName = model.FullName.Trim(), EnrollmentNumber = employee,
            NormalizedEnrollmentNumber = employee, UserName = employee,
            Email = email, EmailConfirmed = true, AccountStatus = AccountStatus.Approved,
            ApprovedAtUtc = DateTimeOffset.UtcNow, MustChangePassword = true
        };
        var created = await users.CreateAsync(user, tempPassword);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Code));
            return View(model);
        }
        var assigned = await users.AddToRoleAsync(user, SystemRoles.Administrator);
        if (!assigned.Succeeded)
        {
            await users.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "No se pudo asignar el rol administrativo. No se creó la cuenta.");
            return View(model);
        }
        await WriteAuditAsync("Administrador.Creado", user, $"Se creó la cuenta administrativa {employee}.");
        TempData["OneTimeCredential"] = $"Cuenta creada. Identificador: {employee}. Contraseña temporal (se muestra una sola vez): {tempPassword}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/block")]
    public Task<IActionResult> Block(string id, CancellationToken token) => Transition(id, AccountStatus.Blocked, token);

    [HttpPost("{id}/unblock")]
    public Task<IActionResult> Unblock(string id, CancellationToken token) => Transition(id, AccountStatus.Approved, token);

    [HttpPost("{id}/delete")]
    public Task<IActionResult> Delete(string id, CancellationToken token) => Transition(id, AccountStatus.Deactivated, token);

    private async Task<IActionResult> Transition(string id, AccountStatus status, CancellationToken token)
    {
        var result = await accounts.ChangeAdministratorStatusAsync(id, status, users.GetUserId(User) ?? "", token);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var admin = await GetAdministratorAsync(id);
        if (admin is null) return NotFound();
        if (admin.AccountStatus is not (AccountStatus.Approved or AccountStatus.Blocked))
        {
            TempData["Error"] = "No se puede restablecer una cuenta eliminada.";
            return RedirectToAction(nameof(Index));
        }
        var reset = await TemporaryPasswordReset.ApplyAsync(users, admin);
        if (reset.Password is null)
        {
            TempData["Error"] = string.Join(" ", reset.Errors.Select(x => TranslateIdentityError(x.Code)));
            return RedirectToAction(nameof(Index));
        }
        await WriteAuditAsync("Administrador.ContrasenaRestablecida", admin, "Contraseña restablecida con credencial temporal de un solo uso.");
        TempData["OneTimeCredential"] = $"Contraseña temporal para {admin.EnrollmentNumber} (se muestra una sola vez): {reset.Password}";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> GetAdministratorAsync(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is not null && await users.IsInRoleAsync(user, SystemRoles.Administrator) ? user : null;
    }

    private async Task WriteAuditAsync(string action, ApplicationUser user, string summary)
    {
        var actor = await users.GetUserAsync(User);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor?.Id, UserDisplayName = actor?.FullName ?? "Superadministrador",
            Action = action, EntityType = "Usuario", EntityId = user.Id, Summary = summary
        });
        await db.SaveChangesAsync();
    }

    private static string TranslateIdentityError(string code) => code switch
    {
        "DuplicateUserName" => "El identificador administrativo ya existe.",
        "DuplicateEmail" => "El correo ya está asociado a otra cuenta.",
        "PasswordTooShort" => "La contraseña generada no cumple la política mínima.",
        _ => "No se pudo completar la operación de cuenta (" + code + ")."
    };
}
