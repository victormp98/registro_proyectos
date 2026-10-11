using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Application.Projects;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class ProjectReviewService(
    PortalDbContext dbContext,
    ProjectDetailsReader detailsReader,
    INotificationService notificationService,
    INotificationPublisher notificationPublisher) : IProjectReviewService
{
    public Task<OperationResult> FinishAsync(int projectId, string administratorId, CancellationToken cancellationToken = default) =>
        ChangeDevelopmentAsync(projectId, administratorId, ProjectDevelopmentStatus.InProgress, ProjectDevelopmentStatus.Completed, cancellationToken);

    public Task<OperationResult> ReopenAsync(int projectId, string administratorId, CancellationToken cancellationToken = default) =>
        ChangeDevelopmentAsync(projectId, administratorId, ProjectDevelopmentStatus.Completed, ProjectDevelopmentStatus.InProgress, cancellationToken);

    private async Task<OperationResult> ChangeDevelopmentAsync(int projectId, string administratorId,
        ProjectDevelopmentStatus expected, ProjectDevelopmentStatus target, CancellationToken cancellationToken)
    {
        var administrator = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == administratorId
            && user.AccountStatus == AccountStatus.Approved
            && dbContext.UserRoles.Any(link => link.UserId == user.Id && dbContext.Roles.Any(role => role.Id == link.RoleId
                && (role.Name == SystemRoles.Administrator || role.Name == SystemRoles.SuperAdministrator))), cancellationToken);
        if (administrator is null) return OperationResult.Failure("Solo administración puede cambiar el estado de desarrollo.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Conditional write serializes competing transitions; a repeated click cannot audit twice.
        var changed = await dbContext.ProjectVersions.Where(version => version.ProjectId == projectId
            && version.DevelopmentStatus == expected
            && dbContext.Projects.Any(project => project.Id == projectId && project.IsActive
                && project.CurrentStatus == ProjectAdministrativeStatus.Approved
                && project.CurrentVersionId == version.Id && project.LatestApprovedVersionId == version.Id))
            .ExecuteUpdateAsync(update => update.SetProperty(version => version.DevelopmentStatus, target), cancellationToken);
        if (changed != 1)
            return OperationResult.Failure("El proyecto no está aprobado o su estado de desarrollo ya cambió. Actualiza el detalle.");

        var completed = target == ProjectDevelopmentStatus.Completed;
        AddAudit(administrator.Id, administrator.FullName, completed ? "Proyecto.Terminado" : "Proyecto.Reabierto", projectId,
            completed ? "Proyecto marcado como terminado." : "Proyecto reabierto: volvió a En proceso.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Success(completed ? "Proyecto marcado como terminado." : "Proyecto reabierto: En proceso.");
    }

    public async Task<IReadOnlyList<AdminProjectListItem>> GetProjectsAsync(AdminProjectFilter filter, CancellationToken cancellationToken = default)
    {
        var query = from project in dbContext.Projects.AsNoTracking()
                    join version in dbContext.ProjectVersions.AsNoTracking() on project.CurrentVersionId equals version.Id
                    join responsible in dbContext.Users.AsNoTracking() on project.ResponsibleUserId equals responsible.Id
                    where project.IsActive && project.CurrentStatus != ProjectAdministrativeStatus.Draft
                    select new { project, version, responsible };

        if (filter.Status.HasValue) query = query.Where(x => x.project.CurrentStatus == filter.Status.Value);
        if (filter.CareerId.HasValue) query = query.Where(x => x.version.Subject != null && x.version.Subject.CareerId == filter.CareerId.Value);
        if (filter.SubjectId.HasValue) query = query.Where(x => x.version.SubjectId == filter.SubjectId.Value);
        if (filter.SchoolCycleId.HasValue) query = query.Where(x => x.version.SchoolCycleId == filter.SchoolCycleId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.version.Name.Contains(term) || x.responsible.FullName.Contains(term) || x.responsible.EnrollmentNumber.Contains(term));
        }

        var rows = await query.Select(x => new
        {
            x.project.Id,
            Name = string.IsNullOrWhiteSpace(x.version.Name) ? "Proyecto sin nombre" : x.version.Name,
            ResponsibleName = x.responsible.FullName,
            ResponsibleStudentNumber = x.responsible.EnrollmentNumber,
            Career = x.version.Subject != null && x.version.Subject.Career != null ? x.version.Subject.Career.Name : "Sin carrera",
            Subject = x.version.Subject != null ? x.version.Subject.Name : "Sin materia",
            SchoolCycle = x.version.SchoolCycle != null ? x.version.SchoolCycle.Name : "Sin ciclo",
            x.project.CurrentStatus,
            x.version.SubmittedAtUtc,
            x.project.AssignedAdministratorId,
            x.version.VersionNumber,
            ReviewNumber = x.project.Reviews.Count
        }).ToListAsync(cancellationToken);

        var administratorIds = rows.Where(x => x.AssignedAdministratorId != null).Select(x => x.AssignedAdministratorId!).Distinct().ToArray();
        var administratorNames = await dbContext.Users.AsNoTracking().Where(x => administratorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        return rows.OrderByDescending(x => x.SubmittedAtUtc)
            .Select(x => new AdminProjectListItem(
                x.Id,
                x.Name,
                x.ResponsibleName,
                x.ResponsibleStudentNumber,
                x.Career,
                x.Subject,
                x.SchoolCycle,
                x.CurrentStatus,
                x.SubmittedAtUtc,
                x.AssignedAdministratorId is not null && administratorNames.TryGetValue(x.AssignedAdministratorId, out var name) ? name : null,
                x.VersionNumber,
                x.ReviewNumber))
            .ToArray();
    }

    public Task<ProjectDetails?> GetProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        detailsReader.ReadAsync(projectId, true, cancellationToken);

    public async Task<IReadOnlyList<ReviewRequirementItem>> GetActiveRequirementsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ReviewRequirements.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ReviewRequirementItem(x.Id, x.Name, x.Description, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<OperationResult> TakeReviewAsync(int projectId, string administratorId, string administratorName, CancellationToken cancellationToken = default)
    {
        var candidate = await dbContext.Projects.AsNoTracking()
            .Where(x => x.Id == projectId && x.IsActive && x.AssignedAdministratorId == null
                && (x.CurrentStatus == ProjectAdministrativeStatus.Submitted || x.CurrentStatus == ProjectAdministrativeStatus.Resubmitted))
            .Select(x => new { x.CurrentStatus, x.CurrentVersionId })
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate is null)
            return OperationResult.Failure("La revisión ya fue tomada por otro administrador o el proyecto cambió de estado.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var changed = await dbContext.Projects
            .Where(x => x.Id == projectId
                && x.IsActive
                && x.AssignedAdministratorId == null
                && (x.CurrentStatus == ProjectAdministrativeStatus.Submitted || x.CurrentStatus == ProjectAdministrativeStatus.Resubmitted))
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.CurrentStatus, ProjectAdministrativeStatus.UnderReview)
                .SetProperty(x => x.AssignedAdministratorId, administratorId)
                .SetProperty(x => x.ReviewStartedAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);
        if (changed != 1)
            return OperationResult.Failure("La revisión ya fue tomada por otro administrador o el proyecto cambió de estado.");

        var review = await dbContext.ProjectReviews
            .Where(x => x.ProjectId == projectId && x.Decision == ProjectReviewDecision.Pending)
            .OrderByDescending(x => x.ReviewNumber)
            .FirstAsync(cancellationToken);
        review.AdministratorId = administratorId;
        review.StartedAtUtc = now;
        AddHistory(projectId, candidate.CurrentVersionId, review.Id, ProjectHistoryEventType.ReviewStarted,
            candidate.CurrentStatus,
            ProjectAdministrativeStatus.UnderReview, administratorId, administratorName, $"Revisión {review.ReviewNumber} asignada.");
        AddAudit(administratorId, administratorName, "Proyecto.RevisionTomada", projectId, $"Se tomó la revisión {review.ReviewNumber}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var responsibleId = await dbContext.Projects.Where(x => x.Id == projectId).Select(x => x.ResponsibleUserId).SingleAsync(cancellationToken);
        var notificationIds = await notificationService.CreateForUserAsync(responsibleId, new NotificationDraft(
            NotificationType.ProjectAssigned, "Proyecto en revisión", $"{administratorName} comenzó a revisar tu proyecto.",
            "Project", projectId.ToString(), $"/Student/Projects/Details/{projectId}", $"project.assigned.{review.Id}"), cancellationToken);
        var resolved = await notificationService.ResolveAdministratorNotificationsAsync(
            NotificationType.ProjectSubmitted, "Project", projectId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notificationService.PublishAsync(notificationIds, cancellationToken);
        await PublishUnreadUpdatesAsync(resolved, cancellationToken);
        return OperationResult.Success("La revisión quedó asignada a tu cuenta.");
    }

    public Task<OperationResult> ApproveAsync(int projectId, string administratorId, string administratorName, string? notes, CancellationToken cancellationToken = default) =>
        CompleteAsync(projectId, administratorId, administratorName, ProjectAdministrativeStatus.Approved, ProjectReviewDecision.Approved, ProjectHistoryEventType.Approved, notes, cancellationToken);

    public Task<OperationResult> RejectAsync(int projectId, string administratorId, string administratorName, string? notes, CancellationToken cancellationToken = default) =>
        CompleteAsync(projectId, administratorId, administratorName, ProjectAdministrativeStatus.Rejected, ProjectReviewDecision.Rejected, ProjectHistoryEventType.Rejected, notes, cancellationToken);

    public async Task<OperationResult> RequestChangesAsync(int projectId, string administratorId, string administratorName, ProjectDecisionData data, CancellationToken cancellationToken = default)
    {
        var notes = Normalize(data.Notes, 2000);
        var requirementIds = data.MissingRequirementIds.Distinct().ToArray();
        if (notes is null && requirementIds.Length == 0)
            return OperationResult.Failure("Escribe una observación o marca al menos un requisito pendiente.");
        var validIds = requirementIds.Length == 0
            ? []
            : await dbContext.ReviewRequirements.Where(x => x.IsActive && requirementIds.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(cancellationToken);
        if (validIds.Length != requirementIds.Length)
            return OperationResult.Failure("Uno de los requisitos seleccionados ya no está disponible.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var changed = await dbContext.Projects
            .Where(x => x.Id == projectId && x.IsActive && x.CurrentStatus == ProjectAdministrativeStatus.UnderReview && x.AssignedAdministratorId == administratorId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.CurrentStatus, ProjectAdministrativeStatus.ChangesRequested)
                .SetProperty(x => x.AssignedAdministratorId, (string?)null)
                .SetProperty(x => x.ReviewStartedAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);
        if (changed != 1)
            return OperationResult.Failure("Solo el administrador asignado puede solicitar correcciones y la revisión debe seguir activa.");

        var project = await dbContext.Projects.SingleAsync(x => x.Id == projectId, cancellationToken);
        var source = await dbContext.ProjectVersions.SingleAsync(x => x.Id == project.CurrentVersionId, cancellationToken);
        var review = await GetPendingReviewAsync(projectId, cancellationToken);
        review.Decision = ProjectReviewDecision.ChangesRequested;
        review.Notes = notes;
        review.CompletedAtUtc = now;
        foreach (var id in validIds)
            review.Requirements.Add(new ProjectReviewRequirement { ReviewRequirementId = id, IsMet = false });

        var editableVersion = StudentProjectService.CopyVersion(source, source.VersionNumber + 1, now);
        dbContext.ProjectVersions.Add(editableVersion);
        await dbContext.SaveChangesAsync(cancellationToken);
        project.CurrentVersionId = editableVersion.Id;
        AddHistory(projectId, source.Id, review.Id, ProjectHistoryEventType.ChangesRequested,
            ProjectAdministrativeStatus.UnderReview, ProjectAdministrativeStatus.ChangesRequested,
            administratorId, administratorName, notes ?? "Se marcaron requisitos pendientes.");
        AddAudit(administratorId, administratorName, "Proyecto.CorreccionesSolicitadas", projectId,
            $"Revisión {review.ReviewNumber}; {validIds.Length} requisito(s) pendiente(s).");
        await dbContext.SaveChangesAsync(cancellationToken);
        var notificationIds = await notificationService.CreateForUserAsync(project.ResponsibleUserId, new NotificationDraft(
            NotificationType.ProjectChangesRequested, "Correcciones solicitadas", notes ?? "Revisa los requisitos marcados en el detalle del proyecto.",
            "Project", projectId.ToString(), $"/Student/Projects/Details/{projectId}", $"project.changes.{review.Id}"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notificationService.PublishAsync(notificationIds, cancellationToken);
        return OperationResult.Success("Las correcciones fueron solicitadas y el alumno puede editar una nueva versión.");
    }

    private async Task<OperationResult> CompleteAsync(
        int projectId,
        string administratorId,
        string administratorName,
        ProjectAdministrativeStatus targetStatus,
        ProjectReviewDecision decision,
        ProjectHistoryEventType eventType,
        string? notes,
        CancellationToken cancellationToken)
    {
        notes = Normalize(notes, 2000);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var changed = await dbContext.Projects
            .Where(x => x.Id == projectId && x.IsActive && x.CurrentStatus == ProjectAdministrativeStatus.UnderReview && x.AssignedAdministratorId == administratorId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.CurrentStatus, targetStatus)
                .SetProperty(x => x.UpdatedAtUtc, now)
                .SetProperty(x => x.LatestApprovedVersionId, x => targetStatus == ProjectAdministrativeStatus.Approved ? x.CurrentVersionId : x.LatestApprovedVersionId), cancellationToken);
        if (changed != 1)
            return OperationResult.Failure("Solo el administrador asignado puede emitir la decisión y la revisión debe seguir activa.");

        var project = await dbContext.Projects.AsNoTracking().SingleAsync(x => x.Id == projectId, cancellationToken);
        var review = await GetPendingReviewAsync(projectId, cancellationToken);
        if (targetStatus == ProjectAdministrativeStatus.Approved)
            await dbContext.ProjectVersions.Where(x => x.Id == project.CurrentVersionId && x.ProjectId == projectId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.DevelopmentStatus, ProjectDevelopmentStatus.InProgress), cancellationToken);
        review.Decision = decision;
        review.Notes = notes;
        review.CompletedAtUtc = now;
        AddHistory(projectId, project.CurrentVersionId, review.Id, eventType,
            ProjectAdministrativeStatus.UnderReview, targetStatus, administratorId, administratorName, notes);
        AddAudit(administratorId, administratorName,
            targetStatus == ProjectAdministrativeStatus.Approved ? "Proyecto.Aprobado" : "Proyecto.Rechazado",
            projectId, $"Se completó la revisión {review.ReviewNumber} como {targetStatus}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var notificationIds = await notificationService.CreateForUserAsync(project.ResponsibleUserId, new NotificationDraft(
            decision == ProjectReviewDecision.Approved ? NotificationType.ProjectApproved : NotificationType.ProjectRejected,
            decision == ProjectReviewDecision.Approved ? "Proyecto aprobado" : "Proyecto rechazado",
            decision == ProjectReviewDecision.Approved ? "Tu proyecto fue aprobado." : notes ?? "Tu proyecto fue rechazado.",
            "Project", projectId.ToString(), $"/Student/Projects/Details/{projectId}", $"project.decision.{review.Id}"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notificationService.PublishAsync(notificationIds, cancellationToken);
        return OperationResult.Success(targetStatus == ProjectAdministrativeStatus.Approved
            ? "Proyecto aprobado. No se publicó automáticamente."
            : "Proyecto rechazado.");
    }

    private async Task PublishUnreadUpdatesAsync(IReadOnlyList<NotificationUnreadUpdate> updates, CancellationToken cancellationToken)
    {
        foreach (var update in updates)
        {
            try { await notificationPublisher.PublishUnreadCountAsync(update.UserId, update.UnreadCount, cancellationToken); }
            catch { /* Persistent state is authoritative; clients refresh from the recent endpoint. */ }
        }
    }

    private async Task<ProjectReview> GetPendingReviewAsync(int projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectReviews.Include(x => x.Requirements)
            .Where(x => x.ProjectId == projectId && x.Decision == ProjectReviewDecision.Pending)
            .OrderByDescending(x => x.ReviewNumber)
            .FirstAsync(cancellationToken);

    private void AddHistory(int projectId, int? versionId, int? reviewId, ProjectHistoryEventType eventType, ProjectAdministrativeStatus? from, ProjectAdministrativeStatus? to, string actorId, string actorName, string? notes) =>
        dbContext.ProjectHistoryEvents.Add(new ProjectHistoryEvent
        {
            ProjectId = projectId,
            ProjectVersionId = versionId,
            ProjectReviewId = reviewId,
            EventType = eventType,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = actorId,
            ActorDisplayName = actorName,
            Notes = Normalize(notes, 800)
        });

    private void AddAudit(string actorId, string actorName, string action, int projectId, string summary) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            UserDisplayName = actorName,
            Action = action,
            EntityType = "Proyecto",
            EntityId = projectId.ToString(),
            Summary = summary
        });

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
}
