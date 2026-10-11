using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Projects;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class ProjectDetailsReader(PortalDbContext dbContext)
{
    public async Task<ProjectDetails?> ReadAsync(int projectId, bool includeInternalHistory = false, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project?.CurrentVersionId is null) return null;

        var version = await dbContext.ProjectVersions.AsNoTracking()
            .Include(x => x.Subject).ThenInclude(x => x!.Career)
            .Include(x => x.ProjectType)
            .Include(x => x.SchoolCycle)
            .SingleOrDefaultAsync(x => x.Id == project.CurrentVersionId.Value, cancellationToken);
        if (version is null) return null;

        var responsible = await dbContext.Users.AsNoTracking()
            .SingleAsync(x => x.Id == project.ResponsibleUserId, cancellationToken);

        var participants = await dbContext.ProjectParticipants.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.Career)
            .Include(x => x.Grade)
            .Include(x => x.AcademicGroup)
            .ToListAsync(cancellationToken);

        var reviewEntities = await dbContext.ProjectReviews.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.ProjectVersion)
            .Include(x => x.Requirements).ThenInclude(x => x.ReviewRequirement)
            .ToListAsync(cancellationToken);

        var administratorIds = reviewEntities.Select(x => x.AdministratorId)
            .Append(project.AssignedAdministratorId)
            .Where(x => x is not null)
            .Distinct()
            .Cast<string>()
            .ToArray();
        var administratorNames = await dbContext.Users.AsNoTracking()
            .Where(x => administratorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        var reviews = reviewEntities.OrderByDescending(x => x.ReviewNumber)
            .Select(x => new ProjectReviewItem(
                x.Id,
                x.ReviewNumber,
                x.ProjectVersion?.VersionNumber ?? 0,
                x.AdministratorId,
                x.AdministratorId is not null && administratorNames.TryGetValue(x.AdministratorId, out var administratorName) ? administratorName : null,
                x.Decision,
                x.Notes,
                x.CreatedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Requirements.OrderBy(r => r.ReviewRequirement!.SortOrder)
                    .Select(r => new ProjectReviewRequirementItem(r.ReviewRequirementId, r.ReviewRequirement!.Name, r.IsMet))
                    .ToArray()))
            .ToArray();

        var versionNumbers = await dbContext.ProjectVersions.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .ToDictionaryAsync(x => x.Id, x => x.VersionNumber, cancellationToken);
        var reviewNumbers = reviewEntities.ToDictionary(x => x.Id, x => x.ReviewNumber);
        var historyEntities = await dbContext.ProjectHistoryEvents.AsNoTracking()
            .Where(x => x.ProjectId == projectId && (includeInternalHistory || x.IsVisibleToStudent))
            .ToListAsync(cancellationToken);
        var history = historyEntities.OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ProjectHistoryItem(
                x.Id,
                x.EventType,
                x.FromStatus,
                x.ToStatus,
                x.ActorDisplayName,
                x.Notes,
                x.ProjectVersionId.HasValue && versionNumbers.TryGetValue(x.ProjectVersionId.Value, out var versionNumber) ? versionNumber : null,
                x.ProjectReviewId.HasValue && reviewNumbers.TryGetValue(x.ProjectReviewId.Value, out var reviewNumber) ? reviewNumber : null,
                x.CreatedAtUtc))
            .ToArray();

        return new ProjectDetails(
            project.Id,
            project.ResponsibleUserId,
            responsible.FullName,
            responsible.EnrollmentNumber,
            version.Subject?.Career?.Name ?? "Sin carrera",
            project.CurrentStatus,
            version.Id,
            version.VersionNumber,
            version.Name,
            version.SubjectId,
            version.Subject?.Name ?? "Sin materia",
            version.ProjectTypeId,
            version.ProjectType?.Name ?? "Sin tipo",
            version.SchoolCycleId,
            version.SchoolCycle?.Name ?? "Sin ciclo",
            version.DevelopmentStatus,
            version.ProblemStatement,
            version.ProposedSolution,
            version.Advisor,
            version.EstimatedCompletionDate,
            project.AssignedAdministratorId,
            project.AssignedAdministratorId is not null && administratorNames.TryGetValue(project.AssignedAdministratorId, out var assignedName) ? assignedName : null,
            project.CreatedAtUtc,
            project.UpdatedAtUtc,
            version.SubmittedAtUtc,
            participants.OrderByDescending(x => x.IsResponsible).ThenBy(x => x.FullName)
                .Select(x => new ProjectParticipantItem(
                    x.Id,
                    x.UserId,
                    x.FullName,
                    x.StudentNumber,
                    x.CareerId,
                    x.Career?.Name ?? "Sin carrera",
                    x.GradeId,
                    x.Grade?.Name ?? "—",
                    x.AcademicGroupId,
                    x.AcademicGroup?.Name ?? "—",
                    x.IsResponsible,
                    x.LinkConfirmed))
                .ToArray(),
            reviews,
            history);
    }
}
