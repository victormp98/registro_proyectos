using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Web.Models.Account;

namespace PortalProyectos.Web.Controllers;

[Route("cuenta")]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IStudentAccountService studentAccountService,
    IRegistrationCatalogService catalogService) : Controller
{
    [HttpGet("crear")]
    public async Task<IActionResult> Register(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboardFromClaims();
        }

        return View(await PopulateCatalogsAsync(new RegisterViewModel(), cancellationToken));
    }

    [HttpPost("crear")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await PopulateCatalogsAsync(model, cancellationToken));
        }

        var result = await studentAccountService.RegisterAsync(
            new RegisterStudentRequest(
                model.FullName,
                model.EnrollmentNumber,
                model.Email,
                model.Password,
                model.CareerId,
                model.GradeId,
                model.AcademicGroupId),
            cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors ?? [result.Message])
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(await PopulateCatalogsAsync(model, cancellationToken));
        }

        var user = await userManager.FindByIdAsync(result.UserId!);
        await signInManager.SignInAsync(user!, isPersistent: false);
        TempData["Success"] = result.Message;
        return RedirectToAction("Index", "Dashboard", new { area = "Student" });
    }

    [HttpGet("iniciar-sesion")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboardFromClaims();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("iniciar-sesion")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var identifier = model.Identifier.Trim();
        ApplicationUser? user;
        if (identifier.Contains('@'))
        {
            user = await userManager.FindByEmailAsync(identifier);
        }
        else
        {
            var normalizedEnrollment = identifier.ToUpperInvariant();
            user = await userManager.Users.SingleOrDefaultAsync(
                x => x.NormalizedEnrollmentNumber == normalizedEnrollment);
        }

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Las credenciales no son válidas.");
            return View(model);
        }

        if (user.AccountStatus is AccountStatus.Blocked or AccountStatus.Deactivated)
        {
            ModelState.AddModelError(string.Empty,
                user.AccountStatus == AccountStatus.Blocked
                    ? "Tu cuenta está bloqueada. Solicita apoyo a administración."
                    : "Tu cuenta fue eliminada. Para volver al sistema debes crear una cuenta nueva.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty,
                result.IsLockedOut
                    ? "Demasiados intentos. Intenta nuevamente en 10 minutos."
                    : "Las credenciales no son válidas.");
            return View(model);
        }

        if (user.MustChangePassword)
        {
            return RedirectToAction(nameof(ChangePassword));
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return await RedirectToDashboardForUserAsync(user);
    }

    [Authorize]
    [HttpGet("cambiar-contrasena")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost("cambiar-contrasena")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        user.MustChangePassword = false;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Tu contraseña fue actualizada.";
        return await RedirectToDashboardForUserAsync(user);
    }

    [Authorize]
    [HttpPost("cerrar-sesion")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("acceso-denegado")]
    public IActionResult AccessDenied() { Response.StatusCode = StatusCodes.Status403Forbidden; return View(); }

    private async Task<RegisterViewModel> PopulateCatalogsAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken)
    {
        var catalogs = await catalogService.GetAsync(cancellationToken);
        model.Careers = catalogs.Careers.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        model.Grades = catalogs.Grades.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        model.Groups = catalogs.Groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
        return model;
    }

    private IActionResult RedirectToDashboardFromClaims() =>
        User.IsInRole(SystemRoles.Student)
            ? RedirectToAction("Index", "Dashboard", new { area = "Student" })
            : RedirectToAction("Index", "Dashboard", new { area = "Admin" });

    private async Task<IActionResult> RedirectToDashboardForUserAsync(ApplicationUser user) =>
        await userManager.IsInRoleAsync(user, SystemRoles.Student)
            ? RedirectToAction("Index", "Dashboard", new { area = "Student" })
            : RedirectToAction("Index", "Dashboard", new { area = "Admin" });
}
