using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Configuration;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class StudentProjectService(
    PortalDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ProjectDetailsReader detailsReader,
    INotificationService notificationService,
    INotificationPublisher notificationPublisher,
    IOptions<ProjectOptions> options) : IStudentProjectService
{
    private static readonly ProjectAdministrativeStatus[] ActiveStatuses =
    [
        ProjectAdministrativeStatus.Draft,
        ProjectAdministrativeStatus.Submitted,
        ProjectAdministrativeStatus.UnderReview,
        ProjectAdministrativeStatus.ChangesRequested,
        ProjectAdministrativeStatus.Resubmitted
    ];

    public ProjectLimits Limits { get; } = new(
        Math.Max(1, options.Value.MaxParticipants),
        Math.Max(1, options.Value.MaxActiveProjects));

    public async Task<StudentProjectDashboardSummary> GetDashboardSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var projects = dbContext.Projects.AsNoTracking().Where(x => x.ResponsibleUserId == userId && x.IsActive);
        return new StudentProjectDashboardSummary(
            await projects.CountAsync(x => ActiveStatuses.Contains(x.CurrentStatus), cancellationToken),
            await projects.CountAsync(x => x.CurrentStatus == ProjectAdministrativeStatus.Submitted
                || x.CurrentStatus == ProjectAdministrativeStatus.Resubmitted
                || x.CurrentStatus == ProjectAdministrativeStatus.UnderReview, cancellationToken),
            await projects.CountAsync(x => x.CurrentStatus == ProjectAdministrativeStatus.ChangesRequested, cancellationToken),
            await projects.CountAsync(x => x.CurrentStatus == ProjectAdministrativeStatus.Approved, cancellationToken),
            await projects.CountAsync(cancellationToken),
            await projects.CountAsync(x => x.CurrentStatus == ProjectAdministrativeStatus.Draft, cancellationToken),
            await dbContext.ProjectPublications.CountAsync(x => x.IsPublished && x.Project != null && x.Project.ResponsibleUserId == userId, cancellationToken),
            await dbContext.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken));
    }

    public async Task<IReadOnlyList<ProjectListItem>> GetProjectsAsync(string userId, ProjectAdministrativeStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = from project in dbContext.Projects.AsNoTracking()
                    join version in dbContext.ProjectVersions.AsNoTracking() on project.CurrentVersionId equals version.Id
                    where project.ResponsibleUserId == userId && project.IsActive
                    select new { project, version };
        if (status.HasValue) query = query.Where(x => x.project.CurrentStatus == status.Value);

        var rows = await query.Select(x => new ProjectListItem(
                x.project.Id,
                string.IsNullOrWhiteSpace(x.version.Name) ? "Proyecto sin nombre" : x.version.Name,
                x.project.CurrentStatus,
                x.version.DevelopmentStatus,
                x.version.Subject != null ? x.version.Subject.Name : "Sin materia",
                x.version.SchoolCycle != null ? x.version.SchoolCycle.Name : "Sin ciclo",
                x.version.VersionNumber,
                x.project.Reviews.Count,
                x.project.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.UpdatedAtUtc).ToArray();
    }

    public async Task<IReadOnlyList<ProjectListItem>> GetLinkedProjectsAsync(string userId, ProjectAdministrativeStatus? status = null, CancellationToken cancellationToken = default)
    {
        if (await GetApprovedStudentAsync(userId) is null) return [];
        var query = from participant in dbContext.ProjectParticipants.AsNoTracking()
                    join project in dbContext.Projects.AsNoTracking() on participant.ProjectId equals project.Id
                    join version in dbContext.ProjectVersions.AsNoTracking() on project.CurrentVersionId equals version.Id
                    where participant.UserId == userId && participant.LinkConfirmed && project.IsActive
                        && project.ResponsibleUserId != userId
                    select new { project, version };
        if (status.HasValue) query = query.Where(x => x.project.CurrentStatus == status.Value);

        var rows = await query.Select(x => new ProjectListItem(
                x.project.Id,
                string.IsNullOrWhiteSpace(x.version.Name) ? "Proyecto sin nombre" : x.version.Name,
                x.project.CurrentStatus,
                x.version.DevelopmentStatus,
                x.version.Subject != null ? x.version.Subject.Name : "Sin materia",
                x.version.SchoolCycle != null ? x.version.SchoolCycle.Name : "Sin ciclo",
                x.version.VersionNumber,
                x.project.Reviews.Count,
                x.project.UpdatedAtUtc,
                true))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id).ToArray();
    }

    public async Task<ProjectDetails?> GetProjectAsync(string userId, int projectId, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Projects.AsNoTracking().AnyAsync(x => x.Id == projectId && x.ResponsibleUserId == userId && x.IsActive, cancellationToken))
            return null;
        return await detailsReader.ReadAsync(projectId, false, cancellationToken);
    }

    public async Task<ProjectDetails?> GetLinkedProjectAsync(string userId, int projectId, CancellationToken cancellationToken = default)
    {
        if (await GetApprovedStudentAsync(userId) is null) return null;
        var linked = await dbContext.ProjectParticipants.AsNoTracking().AnyAsync(x => x.ProjectId == projectId
            && x.UserId == userId && x.LinkConfirmed
            && x.Project != null && x.Project.IsActive && x.Project.ResponsibleUserId != userId, cancellationToken);
        if (!linked) return null;
        var details = await detailsReader.ReadAsync(projectId, false, cancellationToken);
        return details is null ? null : details with { IsResponsible = false };
    }

    public async Task<OperationResult<int>> CreateDraftAsync(string userId, string actorName, CancellationToken cancellationToken = default)
    {
        var user = await GetApprovedStudentAsync(userId);
        if (user is null)
            return OperationResult<int>.Failure("Solo una cuenta de alumno aprobada puede crear proyectos.");
        if (user.CareerId is null || user.GradeId is null || user.AcademicGroupId is null)
            return OperationResult<int>.Failure("Completa tus datos académicos antes de crear un proyecto.");

        var activeCount = await dbContext.Projects.CountAsync(
            x => x.ResponsibleUserId == userId && x.IsActive && ActiveStatuses.Contains(x.CurrentStatus), cancellationToken);
        if (activeCount >= Limits.MaxActiveProjects)
            return OperationResult<int>.Failure($"Alcanzaste el límite de {Limits.MaxActiveProjects} proyectos activos.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            ResponsibleUserId = userId,
            CurrentStatus = ProjectAdministrativeStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        var version = new ProjectVersion
        {
            ProjectId = project.Id,
            VersionNumber = 1,
            Name = string.Empty,
            ProblemStatement = string.Empty,
            ProposedSolution = string.Empty,
            DevelopmentStatus = ProjectDevelopmentStatus.InProgress,
            CreatedAtUtc = now
        };
        dbContext.ProjectVersions.Add(version);
        dbContext.ProjectParticipants.Add(new ProjectParticipant
        {
            ProjectId = project.Id,
            UserId = user.Id,
            FullName = user.FullName,
            StudentNumber = user.EnrollmentNumber,
            CareerId = user.CareerId.Value,
            GradeId = user.GradeId.Value,
            AcademicGroupId = user.AcademicGroupId.Value,
            IsResponsible = true,
            LinkConfirmed = true,
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        project.CurrentVersionId = version.Id;
        AddHistory(project.Id, version.Id, null, ProjectHistoryEventType.Created, null, ProjectAdministrativeStatus.Draft, userId, actorName, "Se creó el borrador.");
        AddAudit(userId, actorName, "Proyecto.Creado", project.Id, "Se creó un proyecto en borrador.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<int>.Success(project.Id, "Borrador creado. Completa la información cuando estés listo.");
    }

    public async Task<OperationResult> UpdateDraftAsync(string userId, int projectId, ProjectDraftData data, CancellationToken cancellationToken = default)
    {
        var user = await GetApprovedStudentAsync(userId);
        if (user is null) return OperationResult.Failure("Tu cuenta debe estar aprobada para editar proyectos.");
        var loaded = await LoadEditableAsync(userId, projectId, cancellationToken);
        if (loaded is null) return OperationResult.Failure("El proyecto no existe, no te pertenece o no puede editarse en su estado actual.");

        var validation = await ValidateDraftSelectionsAsync(user, data, cancellationToken);
        if (!validation.Succeeded) return validation;
        if ((data.Name?.Trim().Length ?? 0) > 120 || (data.ProblemStatement?.Trim().Length ?? 0) > 3000 || (data.ProposedSolution?.Trim().Length ?? 0) > 3000 || (data.Advisor?.Trim().Length ?? 0) > 160)
            return OperationResult.Failure("Uno o más campos superan la longitud permitida.");
        loaded.Value.Version.Name = data.Name?.Trim() ?? string.Empty;
        loaded.Value.Version.SubjectId = data.SubjectId;
        loaded.Value.Version.ProjectTypeId = data.ProjectTypeId;
        loaded.Value.Version.SchoolCycleId = data.SchoolCycleId;
        loaded.Value.Version.ProblemStatement = data.ProblemStatement?.Trim() ?? string.Empty;
        loaded.Value.Version.ProposedSolution = data.ProposedSolution?.Trim() ?? string.Empty;
        loaded.Value.Version.Advisor = Normalize(data.Advisor, 160);
        loaded.Value.Version.EstimatedCompletionDate = data.EstimatedCompletionDate;
        loaded.Value.Project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success("Borrador guardado correctamente.");
    }

    public async Task<OperationResult> DeleteDraftAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default)
    {
        var user = await GetApprovedStudentAsync(userId);
        if (user is null) return OperationResult.Failure("Tu cuenta debe estar aprobada.");
        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && x.ResponsibleUserId == userId && x.IsActive, cancellationToken);
        var mediaFiles = project is null ? [] : await dbContext.MediaFiles.Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
        if (project is null || project.CurrentStatus != ProjectAdministrativeStatus.Draft
            || await dbContext.ProjectReviews.AnyAsync(x => x.ProjectId == projectId, cancellationToken)
            || mediaFiles.Any(x => !x.IsDeleted))
            return OperationResult.Failure("Solo pueden eliminarse borradores propios que nunca se hayan enviado y no tengan documentos adjuntos. Elimina el PDF antes.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        project.CurrentVersionId = null;
        project.LatestApprovedVersionId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.MediaFiles.RemoveRange(mediaFiles);
        dbContext.Projects.Remove(project);
        AddAudit(userId, actorName, "Proyecto.BorradorEliminado", projectId, "Se eliminó un borrador sin enviar.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Success("El borrador fue eliminado.");
    }

    public async Task<OperationResult<LinkedStudentCandidate>> FindStudentAsync(string userId, int projectId, string studentNumber, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadParticipantEditableAsync(userId, projectId, cancellationToken);
        if (await GetApprovedStudentAsync(userId) is null || loaded is null)
            return OperationResult<LinkedStudentCandidate>.Failure("No puedes administrar participantes de este proyecto.");
        if (!HasSavedProjectBasics(loaded.Value.Version))
            return OperationResult<LinkedStudentCandidate>.Failure("Guarda primero el proyecto como borrador para agregar participantes.");

        var normalized = studentNumber.Trim().ToUpperInvariant();
        var candidateUser = await dbContext.Users.AsNoTracking()
            .Include(x => x.Career)
            .Include(x => x.Grade)
            .Include(x => x.AcademicGroup)
            .SingleOrDefaultAsync(x => x.NormalizedEnrollmentNumber == normalized, cancellationToken);
        if (candidateUser is null || !await userManager.IsInRoleAsync(candidateUser, SystemRoles.Student))
            return OperationResult<LinkedStudentCandidate>.Failure("El participante debe registrarse primero en el Portal de Proyectos.");
        if (candidateUser.AccountStatus != AccountStatus.Approved)
        {
            var statusMessage = candidateUser.AccountStatus == AccountStatus.Pending
                ? "La cuenta del participante todavía está pendiente; debe ser aprobada antes de poder agregarlo."
                : "La cuenta del participante no está aprobada y debe regularizarse antes de poder agregarlo.";
            return OperationResult<LinkedStudentCandidate>.Failure(statusMessage);
        }
        if (candidateUser.CareerId is null || candidateUser.GradeId is null || candidateUser.AcademicGroupId is null)
            return OperationResult<LinkedStudentCandidate>.Failure("El participante debe completar sus datos académicos antes de poder agregarlo.");
        var candidate = new LinkedStudentCandidate(
            candidateUser.Id,
            candidateUser.FullName,
            candidateUser.EnrollmentNumber,
            candidateUser.Career?.Name ?? "Sin carrera",
            candidateUser.Grade?.Name ?? "—",
            candidateUser.AcademicGroup?.Name ?? "—",
            candidateUser.AccountStatus);
        if (await dbContext.ProjectParticipants.AnyAsync(x => x.ProjectId == projectId && (x.UserId == candidate.UserId || x.StudentNumber == candidate.StudentNumber), cancellationToken))
            return OperationResult<LinkedStudentCandidate>.Failure("Ese alumno ya participa en el proyecto.");
        return OperationResult<LinkedStudentCandidate>.Success(candidate, "Alumno encontrado. Confirma sus datos antes de vincularlo.");
    }

    public async Task<OperationResult> AddLinkedParticipantAsync(string userId, string actorName, int projectId, string linkedUserId, CancellationToken cancellationToken = default)
    {
        var owner = await GetApprovedStudentAsync(userId);
        if (owner is null) return OperationResult.Failure("Tu cuenta debe estar aprobada.");
        var loaded = await LoadParticipantEditableAsync(userId, projectId, cancellationToken);
        if (loaded is null) return OperationResult.Failure("El proyecto no puede modificarse.");
        if (!HasSavedProjectBasics(loaded.Value.Version))
            return OperationResult.Failure("Guarda primero el proyecto como borrador para agregar participantes.");
        if (!HasSavedProjectBasics(loaded.Value.Version))
            return OperationResult.Failure("Guarda primero el proyecto como borrador para agregar participantes.");
        if (await dbContext.ProjectParticipants.CountAsync(x => x.ProjectId == projectId, cancellationToken) >= Limits.MaxParticipants)
            return OperationResult.Failure($"El proyecto admite como máximo {Limits.MaxParticipants} participantes.");

        var candidate = await userManager.FindByIdAsync(linkedUserId);
        if (candidate is null || !await userManager.IsInRoleAsync(candidate, SystemRoles.Student))
            return OperationResult.Failure("El participante debe registrarse primero en el Portal de Proyectos.");
        if (candidate.AccountStatus != AccountStatus.Approved)
            return OperationResult.Failure(candidate.AccountStatus == AccountStatus.Pending
                ? "La cuenta del participante todavía está pendiente; debe ser aprobada antes de poder agregarlo."
                : "La cuenta del participante no está aprobada y debe regularizarse antes de poder agregarlo.");
        if (candidate.CareerId is null || candidate.GradeId is null || candidate.AcademicGroupId is null)
            return OperationResult.Failure("El participante debe completar sus datos académicos antes de poder agregarlo.");
        if (await dbContext.ProjectParticipants.AnyAsync(x => x.ProjectId == projectId && (x.UserId == candidate.Id || x.StudentNumber == candidate.EnrollmentNumber), cancellationToken))
            return OperationResult.Failure("Ese alumno ya participa en el proyecto.");

        dbContext.ProjectParticipants.Add(new ProjectParticipant
        {
            ProjectId = projectId,
            UserId = candidate.Id,
            FullName = candidate.FullName,
            StudentNumber = candidate.EnrollmentNumber,
            CareerId = candidate.CareerId.Value,
            GradeId = candidate.GradeId.Value,
            AcademicGroupId = candidate.AcademicGroupId.Value,
            LinkConfirmed = true
        });
        loaded.Value.Project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        AddHistory(projectId, loaded.Value.Version.Id, null, ProjectHistoryEventType.ParticipantAdded, loaded.Value.Project.CurrentStatus, loaded.Value.Project.CurrentStatus, userId, actorName, $"Se vinculó a {candidate.FullName} con confirmación explícita.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success("Alumno vinculado como participante.");
    }

    public async Task<OperationResult> RemoveParticipantAsync(string userId, string actorName, int projectId, int participantId, CancellationToken cancellationToken = default)
    {
        if (await GetApprovedStudentAsync(userId) is null) return OperationResult.Failure("Tu cuenta debe estar aprobada.");
        var loaded = await LoadParticipantEditableAsync(userId, projectId, cancellationToken);
        if (loaded is null) return OperationResult.Failure("El proyecto no puede modificarse.");
        if (!HasSavedProjectBasics(loaded.Value.Version))
            return OperationResult.Failure("Guarda primero el proyecto como borrador para administrar participantes.");
        var participant = await dbContext.ProjectParticipants.SingleOrDefaultAsync(x => x.Id == participantId && x.ProjectId == projectId, cancellationToken);
        if (participant is null) return OperationResult.Failure("No se encontró el participante.");
        if (participant.IsResponsible) return OperationResult.Failure("El responsable principal no puede eliminarse.");

        dbContext.ProjectParticipants.Remove(participant);
        loaded.Value.Project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        AddHistory(projectId, loaded.Value.Version.Id, null, ProjectHistoryEventType.ParticipantRemoved, loaded.Value.Project.CurrentStatus, loaded.Value.Project.CurrentStatus, userId, actorName, $"Se retiró al participante {participant.FullName}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success("Participante eliminado.");
    }

    public async Task<OperationResult> SubmitAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default)
    {
        var user = await GetApprovedStudentAsync(userId);
        if (user is null) return OperationResult.Failure("Tu cuenta debe estar aprobada para enviar proyectos.");
        var loaded = await LoadEditableAsync(userId, projectId, cancellationToken);
        if (loaded is null) return OperationResult.Failure("El proyecto no está disponible para envío o no te pertenece.");

        var errors = await ValidateForSubmissionAsync(user, loaded.Value.Project, loaded.Value.Version, cancellationToken);
        if (errors.Count > 0) return OperationResult.Failure(string.Join(" ", errors));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var previousStatus = loaded.Value.Project.CurrentStatus;
        var targetStatus = previousStatus == ProjectAdministrativeStatus.Draft
            ? ProjectAdministrativeStatus.Submitted
            : ProjectAdministrativeStatus.Resubmitted;
        var reviewNumber = await dbContext.ProjectReviews.CountAsync(x => x.ProjectId == projectId, cancellationToken) + 1;
        var review = new ProjectReview
        {
            ProjectId = projectId,
            ProjectVersionId = loaded.Value.Version.Id,
            ReviewNumber = reviewNumber,
            Decision = ProjectReviewDecision.Pending,
            CreatedAtUtc = now
        };
        dbContext.ProjectReviews.Add(review);
        loaded.Value.Version.SubmittedAtUtc = now;
        loaded.Value.Project.CurrentStatus = targetStatus;
        loaded.Value.Project.AssignedAdministratorId = null;
        loaded.Value.Project.ReviewStartedAtUtc = null;
        loaded.Value.Project.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var eventType = targetStatus == ProjectAdministrativeStatus.Submitted ? ProjectHistoryEventType.Submitted : ProjectHistoryEventType.Resubmitted;
        AddHistory(projectId, loaded.Value.Version.Id, review.Id, eventType, previousStatus, targetStatus, userId, actorName, $"Proyecto enviado como revisión {reviewNumber}.");
        AddAudit(userId, actorName, targetStatus == ProjectAdministrativeStatus.Submitted ? "Proyecto.Enviado" : "Proyecto.Reenviado", projectId, $"Versión {loaded.Value.Version.VersionNumber}; revisión {reviewNumber}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var notificationIds = await notificationService.CreateForAdministratorsAsync(new NotificationDraft(
            NotificationType.ProjectSubmitted,
            targetStatus == ProjectAdministrativeStatus.Submitted ? "Proyecto enviado a revisión" : "Proyecto reenviado",
            $"{loaded.Value.Version.Name} · {actorName}",
            "Project", projectId.ToString(), $"/Admin/Projects/Details/{projectId}", $"project.review.{projectId}.{reviewNumber}"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notificationService.PublishAsync(notificationIds, cancellationToken);
        return OperationResult.Success(targetStatus == ProjectAdministrativeStatus.Submitted ? "Proyecto enviado a revisión." : "Correcciones reenviadas para una nueva revisión.");
    }

    public async Task<OperationResult> CancelSubmissionAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default)
    {
        var user = await GetApprovedStudentAsync(userId);
        if (user is null) return OperationResult.Failure("Tu cuenta debe estar aprobada.");
        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && x.ResponsibleUserId == userId && x.IsActive, cancellationToken);
        if (project?.CurrentVersionId is null || project.CurrentStatus != ProjectAdministrativeStatus.Submitted || project.AssignedAdministratorId is not null)
            return OperationResult.Failure("El envío ya no puede cancelarse porque no está disponible o un administrador comenzó la revisión.");
        var source = await dbContext.ProjectVersions.SingleAsync(x => x.Id == project.CurrentVersionId.Value, cancellationToken);
        var review = await dbContext.ProjectReviews.SingleAsync(x => x.ProjectId == projectId && x.ProjectVersionId == source.Id && x.Decision == ProjectReviewDecision.Pending, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        review.Decision = ProjectReviewDecision.Cancelled;
        review.CompletedAtUtc = now;
        review.Notes = "Envío cancelado por el responsable antes de iniciar la revisión.";
        var draftVersion = CopyVersion(source, source.VersionNumber + 1, now);
        dbContext.ProjectVersions.Add(draftVersion);
        await dbContext.SaveChangesAsync(cancellationToken);

        project.CurrentVersionId = draftVersion.Id;
        project.CurrentStatus = ProjectAdministrativeStatus.Draft;
        project.UpdatedAtUtc = now;
        AddHistory(projectId, source.Id, review.Id, ProjectHistoryEventType.SubmissionCancelled, ProjectAdministrativeStatus.Submitted, ProjectAdministrativeStatus.Draft, userId, actorName, "El responsable canceló el envío antes de que fuera tomado.");
        AddAudit(userId, actorName, "Proyecto.EnvioCancelado", projectId, $"Se canceló la revisión {review.ReviewNumber}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var resolved = await notificationService.ResolveAdministratorNotificationsAsync(
            NotificationType.ProjectSubmitted, "Project", projectId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var update in resolved)
        {
            try { await notificationPublisher.PublishUnreadCountAsync(update.UserId, update.UnreadCount, cancellationToken); }
            catch { /* Notifications remain persisted as read; clients refresh when they open the bell. */ }
        }
        return OperationResult.Success("El envío fue cancelado y se creó un nuevo borrador editable.");
    }

    private async Task<ApplicationUser?> GetApprovedStudentAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is not null && user.AccountStatus == AccountStatus.Approved && await userManager.IsInRoleAsync(user, SystemRoles.Student)
            ? user
            : null;
    }

    private async Task<(Project Project, ProjectVersion Version)?> LoadEditableAsync(string userId, int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && x.ResponsibleUserId == userId && x.IsActive, cancellationToken);
        if (project?.CurrentVersionId is null || project.CurrentStatus is not (ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested))
            return null;
        var version = await dbContext.ProjectVersions.SingleAsync(x => x.Id == project.CurrentVersionId.Value, cancellationToken);
        return (project, version);
    }

    private async Task<(Project Project, ProjectVersion Version)?> LoadParticipantEditableAsync(string userId, int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && x.ResponsibleUserId == userId && x.IsActive, cancellationToken);
        if (project?.CurrentVersionId is null || project.CurrentStatus is not (ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested or ProjectAdministrativeStatus.Approved))
            return null;
        var version = await dbContext.ProjectVersions.SingleAsync(x => x.Id == project.CurrentVersionId.Value, cancellationToken);
        return (project, version);
    }

    private async Task<OperationResult> ValidateDraftSelectionsAsync(ApplicationUser user, ProjectDraftData data, CancellationToken cancellationToken)
    {
        if (data.SubjectId.HasValue && !await dbContext.Subjects.AnyAsync(x => x.Id == data.SubjectId && x.IsActive && x.CareerId == user.CareerId, cancellationToken))
            return OperationResult.Failure("La materia no está activa o no corresponde a tu carrera.");
        if (data.ProjectTypeId.HasValue && !await dbContext.ProjectTypes.AnyAsync(x => x.Id == data.ProjectTypeId && x.IsActive, cancellationToken))
            return OperationResult.Failure("El tipo de proyecto no está activo.");
        if (data.SchoolCycleId.HasValue && !await dbContext.SchoolCycles.AnyAsync(x => x.Id == data.SchoolCycleId && x.IsActive, cancellationToken))
            return OperationResult.Failure("El ciclo escolar no está activo.");
        return OperationResult.Success(string.Empty);
    }

    private async Task<List<string>> ValidateForSubmissionAsync(ApplicationUser user, Project project, ProjectVersion version, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (version.Name.Trim().Length is < 5 or > 120) errors.Add("El nombre debe tener entre 5 y 120 caracteres.");
        if (version.ProblemStatement.Trim().Length is < 100 or > 3000) errors.Add("El planteamiento debe tener entre 100 y 3000 caracteres.");
        if (version.ProposedSolution.Trim().Length is < 100 or > 3000) errors.Add("La solución debe tener entre 100 y 3000 caracteres.");
        if (version.SubjectId is null || !await dbContext.Subjects.AnyAsync(x => x.Id == version.SubjectId && x.IsActive && x.CareerId == user.CareerId, cancellationToken)) errors.Add("Selecciona una materia activa de tu carrera.");
        if (version.ProjectTypeId is null || !await dbContext.ProjectTypes.AnyAsync(x => x.Id == version.ProjectTypeId && x.IsActive, cancellationToken)) errors.Add("Selecciona un tipo de proyecto activo.");
        if (version.SchoolCycleId is null || !await dbContext.SchoolCycles.AnyAsync(x => x.Id == version.SchoolCycleId && x.IsActive, cancellationToken)) errors.Add("Selecciona un ciclo escolar activo.");
        if (!Enum.IsDefined(version.DevelopmentStatus)) errors.Add("Selecciona un estado de desarrollo válido.");

        var participants = await dbContext.ProjectParticipants.Where(x => x.ProjectId == project.Id).ToListAsync(cancellationToken);
        if (participants.Count == 0 || participants.All(x => !x.IsResponsible)) errors.Add("El proyecto debe incluir al responsable principal.");
        if (participants.Count > Limits.MaxParticipants) errors.Add($"Solo se permiten {Limits.MaxParticipants} participantes.");
        foreach (var participant in participants)
        {
            if (!await ParticipantCatalogsAreValidAsync(participant.CareerId, participant.GradeId, participant.AcademicGroupId, cancellationToken))
            {
                errors.Add($"Los datos académicos de {participant.FullName} ya no son válidos.");
                break;
            }
        }
        return errors;
    }

    private async Task<bool> ParticipantCatalogsAreValidAsync(int careerId, int gradeId, int groupId, CancellationToken cancellationToken) =>
        await dbContext.Careers.AnyAsync(x => x.Id == careerId && x.IsActive, cancellationToken)
        && await dbContext.Grades.AnyAsync(x => x.Id == gradeId && x.IsActive, cancellationToken)
        && await dbContext.AcademicGroups.AnyAsync(x => x.Id == groupId && x.IsActive, cancellationToken);

    internal static ProjectVersion CopyVersion(ProjectVersion source, int versionNumber, DateTimeOffset now) => new()
    {
        ProjectId = source.ProjectId,
        VersionNumber = versionNumber,
        Name = source.Name,
        SubjectId = source.SubjectId,
        ProjectTypeId = source.ProjectTypeId,
        SchoolCycleId = source.SchoolCycleId,
        DevelopmentStatus = source.DevelopmentStatus,
        ProblemStatement = source.ProblemStatement,
        ProposedSolution = source.ProposedSolution,
        Advisor = source.Advisor,
        EstimatedCompletionDate = source.EstimatedCompletionDate,
        CreatedAtUtc = now
    };

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

    private static bool HasSavedProjectBasics(ProjectVersion version) =>
        !string.IsNullOrWhiteSpace(version.Name)
        && version.SubjectId.HasValue
        && version.ProjectTypeId.HasValue
        && version.SchoolCycleId.HasValue;
}
