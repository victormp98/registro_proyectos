using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;
using PortalProyectos.Web.Areas.Admin.Controllers;
using PortalProyectos.Web.Models.Account;
using PortalProyectos.Web.Models.Admin;

internal static class AccountLifecycleTests
{
    public static async Task RunAsync(ServiceProvider provider, string adminId, string superId, int career, int grade, int group)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PortalDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var accounts = services.GetRequiredService<IAccountAdministrationService>();
        var students = services.GetRequiredService<IStudentAccountService>();
        var projects = services.GetRequiredService<IStudentProjectService>();
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("CUENTAS: " + name);
            Console.WriteLine("PASS CUENTAS: " + name);
        }
        RegisterStudentRequest Request(string key) => new("Flujo alumno " + key, key, key.ToLowerInvariant() + "@flow.test", "Functional!2026", career, grade, group);
        async Task<ApplicationUser> Read(string id)
        {
            db.ChangeTracker.Clear();
            return await users.FindByIdAsync(id) ?? throw new InvalidOperationException("Fixture ausente");
        }
        async Task<bool> DeniedLogin(ApplicationUser user)
        {
            var controller = new PortalProyectos.Web.Controllers.AccountController(users, null!, null!, null!);
            var result = await controller.Login(new LoginViewModel { Identifier = user.EnrollmentNumber, Password = "Functional!2026" });
            return result is ViewResult && !controller.ModelState.IsValid;
        }

        var request = Request("FLOWSTUDENT");
        var registration = await students.RegisterAsync(request);
        var studentId = registration.UserId!;
        Check(registration.Succeeded && (await Read(studentId)).AccountStatus == AccountStatus.Pending, "Registro nuevo siempre Pendiente");
        Check(!(await accounts.ChangeStatusAsync(studentId, AccountStatus.Blocked, null, adminId, "Admin")).Succeeded, "Pendiente no permite bloqueo");
        Check(!(await accounts.ChangeStatusAsync(studentId, AccountStatus.Deactivated, null, adminId, "Admin")).Succeeded, "Pendiente no permite eliminación de cuenta histórica");
        Check(!(await accounts.UnblockAsync(studentId, adminId, "Admin")).Succeeded, "Pendiente no permite desbloqueo");
        Check((await accounts.ChangeStatusAsync(studentId, AccountStatus.Approved, null, adminId, "Admin")).Succeeded, "Pendiente permite aprobar");
        Check(!(await accounts.ChangeStatusAsync(studentId, AccountStatus.Approved, null, adminId, "Admin")).Succeeded
            && !(await accounts.ChangeStatusAsync(studentId, AccountStatus.Rejected, null, adminId, "Admin")).Succeeded, "Aprobada no permite aprobar ni rechazar");
        Check(!(await accounts.ChangeStatusAsync(studentId, AccountStatus.Pending, null, adminId, "Admin")).Succeeded
            && !(await accounts.UnblockAsync(studentId, adminId, "Admin")).Succeeded, "Aprobada no vuelve a Pendiente ni admite desbloqueo");
        var stamp = (await Read(studentId)).SecurityStamp;
        Check((await accounts.ChangeStatusAsync(studentId, AccountStatus.Blocked, null, adminId, "Admin")).Succeeded
            && (await Read(studentId)).SecurityStamp != stamp, "Bloqueo revoca sesiones mediante SecurityStamp");
        Check(await DeniedLogin(await Read(studentId)), "Bloqueada no puede iniciar sesión");
        Check(!(await accounts.ChangeStatusAsync(studentId, AccountStatus.Approved, null, adminId, "Admin")).Succeeded, "Endpoint Aprobar no sustituye Desbloquear");
        Check((await accounts.UnblockAsync(studentId, adminId, "Admin")).Succeeded
            && (await Read(studentId)).AccountStatus == AccountStatus.Approved, "Desbloquear restaura Aprobada, nunca Pendiente");
        var draft = await projects.CreateDraftAsync(studentId, "Flujo alumno");
        Check(draft.Succeeded, "Preparación de historia real para eliminación lógica");
        var beforeAudit = await db.AuditLogs.CountAsync();
        Check((await accounts.ChangeStatusAsync(studentId, AccountStatus.Deactivated, null, adminId, "Admin")).Succeeded, "Aprobada puede eliminarse");
        var tombstone = await Read(studentId);
        Check(tombstone.AccountStatus == AccountStatus.Deactivated && tombstone.PasswordHash is null
            && tombstone.EnrollmentNumber.Length <= 32 && tombstone.Email != request.Email
            && tombstone.NormalizedUserName != request.EnrollmentNumber && tombstone.NormalizedEmail != request.Email.ToUpperInvariant(), "Tombstone libera identificadores y retira credencial");
        Check(await DeniedLogin(tombstone), "Eliminada no puede iniciar sesión");
        Check(await db.Projects.AnyAsync(x => x.Id == draft.Value && x.ResponsibleUserId == studentId)
            && await db.ProjectParticipants.AnyAsync(x => x.ProjectId == draft.Value && x.UserId == studentId)
            && await db.AuditLogs.CountAsync() > beforeAudit, "Eliminación conserva proyecto, participación e historial");
        foreach (var status in Enum.GetValues<AccountStatus>())
            Check(!(await accounts.ChangeStatusAsync(studentId, status, null, adminId, "Admin")).Succeeded, "Eliminada rechaza transición manipulada a " + status);
        Check(!(await accounts.UnblockAsync(studentId, adminId, "Admin")).Succeeded, "Eliminada no admite reactivación por Desbloquear");
        var reregistered = await students.RegisterAsync(request);
        Check(reregistered.Succeeded && reregistered.UserId != studentId
            && (await Read(reregistered.UserId!)).AccountStatus == AccountStatus.Pending, "Misma matrícula/correo crean nueva solicitud y nuevo UserId");
        Check(!(await projects.GetProjectsAsync(reregistered.UserId!)).Any()
            && (await users.GetRolesAsync(await Read(reregistered.UserId!))).SequenceEqual(new[] { SystemRoles.Student }), "Nueva cuenta no hereda proyectos ni roles históricos");

        var rejectedRequest = Request("FLOWREJECT");
        var rejectRegistration = await students.RegisterAsync(rejectedRequest);
        var rejectId = rejectRegistration.UserId!;
        await services.GetRequiredService<INotificationService>().CreateForUserAsync(rejectId,
            new NotificationDraft(NotificationType.AccountPending, "Solicitud", "Prueba aislada", "User", rejectId, "/Student", "reject-own." + rejectId));
        var otherNotices = await db.Notifications.CountAsync(x => x.UserId != rejectId);
        var rejectResult = await accounts.ChangeStatusAsync(rejectId, AccountStatus.Rejected, "Datos por corregir", adminId, "Admin");
        Check(rejectResult.Succeeded && !await db.Users.AnyAsync(x => x.Id == rejectId), "Rechazar retira físicamente solo la solicitud pendiente: " + rejectResult.Message);
        Check(!await db.Notifications.AnyAsync(x => x.UserId == rejectId)
            && !await db.UserRoles.AnyAsync(x => x.UserId == rejectId)
            && await db.Notifications.CountAsync(x => x.UserId != rejectId) == otherNotices, "Rechazo limpia dependientes propios sin borrar avisos ajenos");
        Check(await db.AuditLogs.AnyAsync(x => x.EntityId == rejectId && x.Action == "Cuenta.Rechazada"), "Bitácora de rechazo sobrevive al borrado");
        var newRejected = await students.RegisterAsync(rejectedRequest);
        Check(newRejected.Succeeded && newRejected.UserId != rejectId
            && (await Read(newRejected.UserId!)).AccountStatus == AccountStatus.Pending, "Rechazo permite registro inmediato con los mismos identificadores");

        var blockedRegistration = await students.RegisterAsync(Request("FLOWBLOCKDELETE"));
        await accounts.ChangeStatusAsync(blockedRegistration.UserId!, AccountStatus.Approved, null, adminId, "Admin");
        await accounts.ChangeStatusAsync(blockedRegistration.UserId!, AccountStatus.Blocked, null, adminId, "Admin");
        Check((await accounts.ChangeStatusAsync(blockedRegistration.UserId!, AccountStatus.Deactivated, null, adminId, "Admin")).Succeeded, "Bloqueada también permite eliminación definitiva");
        Check(!(await accounts.ChangeStatusAsync(newRejected.UserId!, AccountStatus.Approved, null, reregistered.UserId!, "Forged")).Succeeded, "Alumno no puede ejecutar administración mediante servicio");

        // Keep useful isolated state examples for browser verification.
        var blockedUi = await students.RegisterAsync(Request("FLOWUIBLOCK"));
        await accounts.ChangeStatusAsync(blockedUi.UserId!, AccountStatus.Approved, null, adminId, "Admin");
        await accounts.ChangeStatusAsync(blockedUi.UserId!, AccountStatus.Blocked, null, adminId, "Admin");
        var approvedUi = await students.RegisterAsync(Request("FLOWUIAPPROVED"));
        await accounts.ChangeStatusAsync(approvedUi.UserId!, AccountStatus.Approved, null, adminId, "Admin");

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, superId) }, "Test")) };
        var controller = new AdministratorsController(db, users, accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new FunctionalTestTempDataProvider())
        };
        async Task<ApplicationUser> CreateAdmin(string identifier)
        {
            await controller.Create(new AdministrativeUserForm { FullName = "Flujo administrador " + identifier, EmployeeNumber = identifier, Email = identifier.ToLowerInvariant() + "@flow.test" });
            return await users.FindByNameAsync(identifier) ?? throw new InvalidOperationException("No se creó administrador");
        }
        var admin = await CreateAdmin("FLOWADMIN");
        var originalAdminId = admin.Id;
        Check(admin.AccountStatus == AccountStatus.Approved && await users.IsInRoleAsync(admin, SystemRoles.Administrator), "Administrador nuevo se crea Activo sin aprobación");
        Check(!(await accounts.ChangeAdministratorStatusAsync(admin.Id, AccountStatus.Pending, superId)).Succeeded
            && !(await accounts.ChangeAdministratorStatusAsync(admin.Id, AccountStatus.Rejected, superId)).Succeeded, "Administrador no tiene Pendiente/Rechazado");
        Check(!(await accounts.ChangeAdministratorStatusAsync(admin.Id, AccountStatus.Blocked, adminId)).Succeeded, "Solo SuperAdmin gestiona administradores");
        var adminHash = admin.PasswordHash;
        Check((await accounts.ChangeAdministratorStatusAsync(admin.Id, AccountStatus.Blocked, superId)).Succeeded
            && await DeniedLogin(await Read(admin.Id)), "Administrador bloqueado pierde acceso");
        Check((await accounts.ChangeAdministratorStatusAsync(admin.Id, AccountStatus.Approved, superId)).Succeeded
            && (await Read(admin.Id)).PasswordHash == adminHash, "Desbloqueo administrativo no cambia contraseña ni identidad");
        Check((await accounts.ChangeAdministratorStatusAsync(admin.Id, AccountStatus.Deactivated, superId)).Succeeded, "Administrador activo permite eliminación lógica");
        Check(await DeniedLogin(await Read(admin.Id))
            && !(await accounts.ChangeAdministratorStatusAsync(admin.Id, AccountStatus.Approved, superId)).Succeeded, "Administrador eliminado no accede ni se reactiva");
        var recreatedAdmin = await CreateAdmin("FLOWADMIN");
        Check(recreatedAdmin.Id != originalAdminId && recreatedAdmin.AccountStatus == AccountStatus.Approved, "Mismo correo/identificador crea administrador distinto y Activo");
        await accounts.ChangeAdministratorStatusAsync(recreatedAdmin.Id, AccountStatus.Blocked, superId);
        Check((await accounts.ChangeAdministratorStatusAsync(recreatedAdmin.Id, AccountStatus.Deactivated, superId)).Succeeded, "Administrador bloqueado también puede eliminarse");
        foreach(var status in Enum.GetValues<AccountStatus>())
            Check(!(await accounts.ChangeAdministratorStatusAsync(recreatedAdmin.Id, status, superId)).Succeeded, "Administrador eliminado rechaza transición a " + status);
        Check(!(await accounts.ChangeAdministratorStatusAsync(superId, AccountStatus.Blocked, superId)).Succeeded
            && !(await accounts.ChangeAdministratorStatusAsync(superId, AccountStatus.Deactivated, superId)).Succeeded, "SuperAdmin no se bloquea/elimina a sí mismo");
        var superUser = await Read(superId);
        await users.AddToRoleAsync(superUser, SystemRoles.Administrator);
        Check(!(await accounts.ChangeAdministratorStatusAsync(superId, AccountStatus.Deactivated, superId)).Succeeded
            && !(await accounts.ChangeStatusAsync(superId, AccountStatus.Blocked, null, adminId, "Admin")).Succeeded
            && (await Read(superId)).AccountStatus == AccountStatus.Approved, "Protección SuperAdmin también con roles mixtos; sistema conserva SuperAdmin activo");
        await users.RemoveFromRoleAsync(await Read(superId), SystemRoles.Administrator);
        Check(services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval == TimeSpan.Zero, "Invalidación de sesiones se aplica en la siguiente petición");
        Check(await db.AuditLogs.AnyAsync(x => x.Action == "Cuenta.Eliminada")
            && await db.AuditLogs.AnyAsync(x => x.Action == "Cuenta.Blocked")
            && await db.AuditLogs.AnyAsync(x => x.Action == "Cuenta.Desbloqueada")
            && await db.AuditLogs.AnyAsync(x => x.Action == "Administrador.Eliminada"), "Acciones quedan en la bitácora existente sin contraseñas");
        await CreateAdmin("FLOWUIACTIVE");
        var adminUi = await CreateAdmin("FLOWUIBLOCKED");
        await accounts.ChangeAdministratorStatusAsync(adminUi.Id, AccountStatus.Blocked, superId);
        Console.WriteLine("ACCOUNT_UI=" + System.Text.Json.JsonSerializer.Serialize(new
        {
            pending = reregistered.UserId,
            rejectedResubmission = newRejected.UserId,
            approved = approvedUi.UserId,
            blocked = blockedUi.UserId,
            deleted = studentId,
            adminActive = (await users.FindByNameAsync("FLOWUIACTIVE"))!.Id,
            adminBlocked = adminUi.Id,
            adminDeleted = originalAdminId,
            superAdmin = superId
        }));
    }
}
