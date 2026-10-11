using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class StudentAccountService(
    PortalDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    INotificationService notificationService) : IStudentAccountService
{
    public async Task<RegistrationResult> RegisterAsync(
        RegisterStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var enrollmentNumber = NormalizeEnrollment(request.EnrollmentNumber);
        var email = request.Email.Trim();

        if (await dbContext.Users.AnyAsync(x => x.NormalizedEnrollmentNumber == enrollmentNumber, cancellationToken))
        {
            return new RegistrationResult(false, "No fue posible crear la cuenta.", Errors: ["La matrícula ya está registrada."]);
        }

        if (!await CatalogSelectionIsValidAsync(request, cancellationToken))
        {
            return new RegistrationResult(false, "No fue posible crear la cuenta.", Errors: ["Selecciona una carrera, grado y grupo vigentes."]);
        }

        var user = new ApplicationUser
        {
            FullName = request.FullName.Trim(),
            EnrollmentNumber = request.EnrollmentNumber.Trim(),
            NormalizedEnrollmentNumber = enrollmentNumber,
            UserName = enrollmentNumber,
            Email = email,
            CareerId = request.CareerId,
            GradeId = request.GradeId,
            AcademicGroupId = request.AcademicGroupId,
            AccountStatus = AccountStatus.Pending
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new RegistrationResult(
                false,
                "No fue posible crear la cuenta.",
                Errors: result.Errors.Select(x => TranslateIdentityError(x.Code, x.Description)).ToArray());
        }

        var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.Student);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new RegistrationResult(false, "No fue posible asignar el perfil de alumno.");
        }

        var notificationIds = await notificationService.CreateForAdministratorsAsync(new NotificationDraft(
            NotificationType.AccountPending,
            "Nueva cuenta pendiente",
            $"{user.FullName} · matrícula {user.EnrollmentNumber}",
            "User", user.Id, "/Admin/Users?status=Pending", $"account.pending.{user.Id}"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notificationService.PublishAsync(notificationIds, cancellationToken);

        return new RegistrationResult(true, "Tu cuenta fue creada y está pendiente de aprobación.", user.Id);
    }

    public async Task<StudentProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default) =>
        await dbContext.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new StudentProfile(
                x.Id,
                x.FullName,
                x.EnrollmentNumber,
                x.Email ?? string.Empty,
                x.CareerId ?? 0,
                x.GradeId ?? 0,
                x.AcademicGroupId ?? 0,
                x.Career != null ? x.Career.Name : "Sin carrera",
                x.Grade != null ? x.Grade.Name : "—",
                x.AcademicGroup != null ? x.AcademicGroup.Name : "—",
                x.AccountStatus,
                x.StatusReason,
                x.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<OperationResult> UpdateProfileAsync(
        string userId,
        UpdateStudentProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult.Failure("No se encontró el perfil.");
        }

        if (!await CatalogSelectionIsValidAsync(request.CareerId, request.GradeId, request.AcademicGroupId, cancellationToken))
        {
            return OperationResult.Failure("Selecciona una carrera, grado y grupo vigentes.");
        }

        var email = request.Email.Trim();
        var emailOwner = await userManager.FindByEmailAsync(email);
        if (emailOwner is not null && emailOwner.Id != user.Id)
        {
            return OperationResult.Failure("El correo ya está registrado.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.NormalizedEmail = userManager.NormalizeEmail(email);
        user.CareerId = request.CareerId;
        user.GradeId = request.GradeId;
        user.AcademicGroupId = request.AcademicGroupId;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? OperationResult.Success("Perfil actualizado correctamente.")
            : OperationResult.Failure(string.Join(" ", result.Errors.Select(x => x.Description)));
    }

    private async Task<bool> CatalogSelectionIsValidAsync(RegisterStudentRequest request, CancellationToken cancellationToken) =>
        await CatalogSelectionIsValidAsync(request.CareerId, request.GradeId, request.AcademicGroupId, cancellationToken);

    private async Task<bool> CatalogSelectionIsValidAsync(int careerId, int gradeId, int groupId, CancellationToken cancellationToken) =>
        await dbContext.Careers.AnyAsync(x => x.Id == careerId && x.IsActive, cancellationToken)
        && await dbContext.Grades.AnyAsync(x => x.Id == gradeId && x.IsActive, cancellationToken)
        && await dbContext.AcademicGroups.AnyAsync(x => x.Id == groupId && x.IsActive, cancellationToken);

    private static string NormalizeEnrollment(string enrollmentNumber) =>
        enrollmentNumber.Trim().ToUpperInvariant();

    private static string TranslateIdentityError(string code, string fallback) => code switch
    {
        "DuplicateEmail" => "El correo ya está registrado.",
        "PasswordTooShort" => "La contraseña debe tener al menos 10 caracteres.",
        "PasswordRequiresDigit" => "La contraseña debe incluir un número.",
        "PasswordRequiresLower" => "La contraseña debe incluir una minúscula.",
        "PasswordRequiresUpper" => "La contraseña debe incluir una mayúscula.",
        "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir un símbolo.",
        _ => fallback
    };
}
