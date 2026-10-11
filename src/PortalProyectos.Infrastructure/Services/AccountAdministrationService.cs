using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Application.Users;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.News;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class AccountAdministrationService(
    PortalDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    INotificationService notificationService,
    INotificationPublisher notificationPublisher) : IAccountAdministrationService
{
    public async Task<IReadOnlyList<AccountReviewItem>> GetStudentsAsync(
        AccountStatus? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var studentRoleId = await dbContext.Roles
            .Where(x => x.Name == SystemRoles.Student)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        var studentIds = dbContext.UserRoles
            .Where(x => x.RoleId == studentRoleId)
            .Select(x => x.UserId);

        var query = dbContext.Users
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.Id));

        if (status.HasValue)
        {
            query = query.Where(x => x.AccountStatus == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.FullName.Contains(term) ||
                x.EnrollmentNumber.Contains(term) ||
                (x.Email != null && x.Email.Contains(term)));
        }

        var users = await query
            .Select(x => new AccountReviewItem(
                x.Id,
                x.FullName,
                x.EnrollmentNumber,
                x.Email ?? string.Empty,
                x.Career != null ? x.Career.Name : "Sin carrera",
                x.Grade != null ? x.Grade.Name : "—",
                x.AcademicGroup != null ? x.AcademicGroup.Name : "—",
                x.AccountStatus,
                x.StatusReason,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        // SQLite no traduce ORDER BY para DateTimeOffset; se ordena tras aplicar filtros en SQL.
        return users.OrderByDescending(x => x.CreatedAtUtc).ToList();
    }

    public async Task<AdminDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var studentRoleId = await dbContext.Roles
            .Where(x => x.Name == SystemRoles.Student)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        var studentIds = dbContext.UserRoles
            .Where(x => x.RoleId == studentRoleId)
            .Select(x => x.UserId);

        var students = dbContext.Users.Where(x => studentIds.Contains(x.Id));

        var projectsWithVersion = dbContext.Projects.AsNoTracking().Where(x => x.IsActive && x.CurrentVersionId != null)
            .Join(dbContext.ProjectVersions.AsNoTracking(), x => x.CurrentVersionId, x => (int?)x.Id, (project, version) => new { project, version });
        var facts = await projectsWithVersion.Select(x => new
        {
            Career = x.version.Subject != null ? x.version.Subject.Career!.Name : "Sin carrera",
            Subject = x.version.Subject != null ? x.version.Subject.Name : "Sin materia",
            Cycle = x.version.SchoolCycle != null ? x.version.SchoolCycle.Name : "Sin ciclo",
            x.version.DevelopmentStatus
        }).ToListAsync(cancellationToken);
        static List<DashboardBreakdown> CountBy(IEnumerable<string> source) => source.GroupBy(x => x).Select(x => new DashboardBreakdown(x.Key, x.Count())).OrderByDescending(x => x.Count).ToList();
        var byCareer = CountBy(facts.Select(x => x.Career));
        var bySubject = CountBy(facts.Select(x => x.Subject));
        var byCycle = CountBy(facts.Select(x => x.Cycle));
        var statusFacts = await dbContext.Projects.AsNoTracking().Where(x => x.IsActive).Select(x => x.CurrentStatus).ToListAsync(cancellationToken);
        var byStatus = statusFacts.GroupBy(x => x).Select(x => new DashboardBreakdown(x.Key.ToString(), x.Count())).OrderByDescending(x => x.Count).ToList();

        return new AdminDashboardSummary(
            await students.CountAsync(x => x.AccountStatus == AccountStatus.Pending, cancellationToken),
            await students.CountAsync(x => x.AccountStatus == AccountStatus.Approved, cancellationToken),
            await students.CountAsync(x => x.AccountStatus == AccountStatus.Rejected, cancellationToken),
            await students.CountAsync(x => x.AccountStatus == AccountStatus.Blocked, cancellationToken),
            await dbContext.Careers.CountAsync(x => x.IsActive, cancellationToken),
            await dbContext.Projects.CountAsync(x => x.IsActive && (x.CurrentStatus == ProjectAdministrativeStatus.Submitted || x.CurrentStatus == ProjectAdministrativeStatus.Resubmitted), cancellationToken),
            await dbContext.Projects.CountAsync(x => x.IsActive && x.CurrentStatus == ProjectAdministrativeStatus.UnderReview, cancellationToken),
            await dbContext.Projects.CountAsync(x => x.IsActive && x.CurrentStatus == ProjectAdministrativeStatus.Approved, cancellationToken),
            await dbContext.ProjectPublications.CountAsync(x => x.IsPublished, cancellationToken),
            await dbContext.ProjectPublications.CountAsync(x => !x.IsPublished && x.HiddenAtUtc != null, cancellationToken),
            await dbContext.ProjectPublications.CountAsync(x => x.IsPublished && x.IsFeatured, cancellationToken),
            await dbContext.ProjectLikes.CountAsync(cancellationToken),
            await students.CountAsync(cancellationToken),
            await dbContext.Projects.CountAsync(x => x.IsActive, cancellationToken),
            await dbContext.Projects.CountAsync(x => x.IsActive && x.CurrentStatus == ProjectAdministrativeStatus.ChangesRequested, cancellationToken),
            await projectsWithVersion.CountAsync(x => x.version.DevelopmentStatus == ProjectDevelopmentStatus.Completed, cancellationToken),
            await dbContext.NewsPosts.CountAsync(x => x.Status == NewsPostStatus.Published, cancellationToken),
            await dbContext.ProjectParticipants.CountAsync(cancellationToken),
            byCareer, bySubject, byCycle, byStatus);
    }

    public Task<OperationResult> ChangeStatusAsync(string userId, AccountStatus newStatus, string? reason,
        string administratorId, string administratorName, CancellationToken cancellationToken = default) =>
        TransitionAsync(userId, newStatus, reason, administratorId, false, false, cancellationToken);

    public Task<OperationResult> UnblockAsync(string userId, string administratorId, string administratorName,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(userId, AccountStatus.Approved, null, administratorId, false, true, cancellationToken);

    public Task<OperationResult> ChangeAdministratorStatusAsync(string userId, AccountStatus newStatus,
        string administratorId, CancellationToken cancellationToken = default) =>
        TransitionAsync(userId, newStatus, null, administratorId, true, newStatus == AccountStatus.Approved, cancellationToken);

    private async Task<OperationResult> TransitionAsync(string userId, AccountStatus target, string? reason,
        string actorId, bool administrativeAccount, bool unblock, CancellationToken token)
    {
        var actor = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.AccountStatus == AccountStatus.Approved, token);
        if (actor is null || !(await userManager.IsInRoleAsync(actor, SystemRoles.SuperAdministrator)
            || (!administrativeAccount && await userManager.IsInRoleAsync(actor, SystemRoles.Administrator))))
            return OperationResult.Failure("No tienes permiso para administrar esta cuenta.");

        var candidate = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, token);
        if (candidate is null || !await userManager.IsInRoleAsync(candidate, administrativeAccount ? SystemRoles.Administrator : SystemRoles.Student)
            || await userManager.IsInRoleAsync(candidate, SystemRoles.SuperAdministrator))
            return OperationResult.Failure("La cuenta no está disponible para esta operación.");
        // Superadministrator accounts are intentionally outside this module, including mixed-role accounts.
        // Therefore neither self-removal nor removal of the last active Superadministrator is possible.
        if (candidate.Id == actorId)
            return OperationResult.Failure("No puedes bloquear ni eliminar tu propia cuenta.");

        var previous = candidate.AccountStatus;
        var valid = target switch
        {
            AccountStatus.Approved => unblock ? previous == AccountStatus.Blocked : !administrativeAccount && previous == AccountStatus.Pending,
            AccountStatus.Rejected => !administrativeAccount && previous == AccountStatus.Pending,
            AccountStatus.Blocked => previous == AccountStatus.Approved,
            AccountStatus.Deactivated => previous is AccountStatus.Approved or AccountStatus.Blocked,
            _ => false
        };
        if (!valid) return OperationResult.Failure("La acción no es válida para el estado actual de la cuenta.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
        // Compare-and-swap acquires the SQLite write lock before processing dependents.
        // Concurrent/repeated requests cannot apply a transition twice.
        var stamp = Guid.NewGuid().ToString();
        var locked = await dbContext.Users.Where(x => x.Id == userId && x.AccountStatus == previous
                && x.ConcurrencyStamp == candidate.ConcurrencyStamp)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ConcurrencyStamp, stamp), token);
        if (locked != 1) return OperationResult.Failure("La cuenta cambió mientras se procesaba la solicitud. Actualiza el listado.");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return OperationResult.Failure("La cuenta ya no existe.");
        await dbContext.Entry(user).ReloadAsync(token);

        if (target == AccountStatus.Rejected && await HasHistoricalActivityAsync(userId, token))
            return OperationResult.Failure("La solicitud tiene referencias históricas inesperadas. No se eliminó ningún dato.");

        var prefix = administrativeAccount ? "Administrador" : "Cuenta";
        var action = unblock ? "Desbloqueada" : target switch
        {
            AccountStatus.Approved => "Approved",
            AccountStatus.Rejected => "Rechazada",
            AccountStatus.Blocked => "Blocked",
            _ => "Eliminada"
        };
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actor.Id, UserDisplayName = actor.FullName, Action = prefix + "." + action,
            EntityType = "Usuario", EntityId = user.Id,
            Summary = target == AccountStatus.Rejected
                ? $"Solicitud rechazada y retirada: {user.FullName}. {NormalizeReason(reason)}".Trim()
                : $"{user.FullName}: {previous} → {(target == AccountStatus.Deactivated ? "Eliminada" : target.ToString())}."
        });

        var resolved = previous == AccountStatus.Pending
            ? await notificationService.ResolveAdministratorNotificationsAsync(
                PortalProyectos.Domain.Notifications.NotificationType.AccountPending, "User", user.Id, token)
            : [];
        IReadOnlyList<long> notificationIds = [];
        IdentityResult result;
        if (target == AccountStatus.Rejected)
        {
            // Recipient notifications belong exclusively to this request. Administrative history remains.
            // The notification service may have tracked these rows in this scoped DbContext;
            // detach them before deleting in SQL so EF won't issue a second stale DELETE via cascade.
            foreach (var trackedNotice in dbContext.ChangeTracker.Entries<PortalProyectos.Domain.Notifications.Notification>()
                         .Where(x => x.Entity.UserId == user.Id).ToArray())
                trackedNotice.State = EntityState.Detached;
            await dbContext.Notifications.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(token);
            dbContext.Users.Remove(user); // Identity-owned rows cascade; all domain references were checked above.
            try
            {
                await dbContext.SaveChangesAsync(token);
            }
            catch (DbUpdateConcurrencyException)
            {
                return OperationResult.Failure("No se pudo retirar la solicitud por una referencia concurrente.");
            }
            result = IdentityResult.Success;
        }
        else
        {
            user.AccountStatus = target;
            user.StatusBeforeBlock = target == AccountStatus.Blocked ? AccountStatus.Approved : null;
            user.StatusReason = target == AccountStatus.Approved ? null : NormalizeReason(reason);
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            if (target == AccountStatus.Approved && !unblock)
            {
                user.ApprovedAtUtc = DateTimeOffset.UtcNow;
                user.ApprovedByUserId = actor.Id;
            }
            user.SecurityStamp = Guid.NewGuid().ToString();
            if (target == AccountStatus.Deactivated)
            {
                string tombstone;
                do { tombstone = "DEL-" + Guid.NewGuid().ToString("N")[..28]; }
                while (await dbContext.Users.AnyAsync(x => x.NormalizedEnrollmentNumber == tombstone.ToUpper()
                    || x.NormalizedUserName == tombstone.ToUpper()
                    || x.NormalizedEmail == (tombstone + "@deleted.invalid").ToUpper(), token));
                user.EnrollmentNumber = tombstone;
                user.NormalizedEnrollmentNumber = tombstone.ToUpperInvariant();
                user.UserName = tombstone;
                user.NormalizedUserName = tombstone.ToUpperInvariant();
                user.Email = tombstone + "@deleted.invalid";
                user.NormalizedEmail = user.Email.ToUpperInvariant();
                user.EmailConfirmed = false;
                user.PasswordHash = null;
                user.PhoneNumber = null;
                user.PhoneNumberConfirmed = false;
                user.TwoFactorEnabled = false;
                user.MustChangePassword = false;
                await dbContext.UserTokens.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(token);
                await dbContext.UserLogins.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(token);
            }
            result = await userManager.UpdateAsync(user);
            if (result.Succeeded && !administrativeAccount && target != AccountStatus.Deactivated)
            {
                var type = unblock ? PortalProyectos.Domain.Notifications.NotificationType.AccountUnblocked
                    : target == AccountStatus.Approved ? PortalProyectos.Domain.Notifications.NotificationType.AccountApproved
                    : PortalProyectos.Domain.Notifications.NotificationType.AccountBlocked;
                var title = unblock ? "Cuenta desbloqueada" : target == AccountStatus.Approved ? "Cuenta aprobada" : "Cuenta bloqueada";
                var message = NormalizeReason(reason) ?? (unblock ? "Tu cuenta fue desbloqueada. Inicia sesión para continuar."
                    : target == AccountStatus.Approved ? "Tu cuenta académica fue aprobada. Ya puedes registrar proyectos."
                    : "Tu cuenta fue bloqueada. Contacta a administración para recibir ayuda.");
                notificationIds = await notificationService.CreateForUserAsync(user.Id, new NotificationDraft(
                    type, title, message, "User", user.Id, "/Student/Dashboard", $"account.transition.{user.Id}.{stamp}"), token);
            }
        }
        if (!result.Succeeded) return OperationResult.Failure("No se pudo completar la operación. " + string.Join(" ", result.Errors.Select(x => x.Description)));
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        await notificationService.PublishAsync(notificationIds, token);
        foreach (var update in resolved)
        {
            try { await notificationPublisher.PublishUnreadCountAsync(update.UserId, update.UnreadCount, token); }
            catch { /* Persisted state remains authoritative. */ }
        }
        return OperationResult.Success(target == AccountStatus.Rejected
            ? "Solicitud rechazada y eliminada. La matrícula y el correo están disponibles para un nuevo registro."
            : target == AccountStatus.Deactivated ? "Cuenta eliminada definitivamente. Sus referencias históricas se conservan."
            : unblock ? "Cuenta desbloqueada." : target == AccountStatus.Blocked ? "Cuenta bloqueada." : "Cuenta aprobada.");
    }

    private async Task<bool> HasHistoricalActivityAsync(string id, CancellationToken token) =>
        await dbContext.Projects.AnyAsync(x => x.ResponsibleUserId == id || x.AssignedAdministratorId == id, token)
        || await dbContext.ProjectParticipants.AnyAsync(x => x.UserId == id, token)
        || await dbContext.ProjectReviews.AnyAsync(x => x.AdministratorId == id, token)
        || await dbContext.ProjectHistoryEvents.AnyAsync(x => x.ActorUserId == id, token)
        || await dbContext.ProjectPublications.AnyAsync(x => x.PublishedByUserId == id, token)
        || await dbContext.ProjectLikes.AnyAsync(x => x.UserId == id, token)
        || await dbContext.MediaFiles.AnyAsync(x => x.UploadedByUserId == id, token)
        || await dbContext.NewsPosts.AnyAsync(x => x.CreatedByUserId == id, token)
        || await dbContext.Users.AnyAsync(x => x.ApprovedByUserId == id, token);

    private static string? NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized[..Math.Min(normalized.Length, 500)];
    }
}
