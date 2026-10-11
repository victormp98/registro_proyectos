using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Users;

namespace PortalProyectos.Application.Projects;

public sealed record ProjectLimits(int MaxParticipants, int MaxActiveProjects);

public sealed record ProjectDraftData(
    string? Name,
    int? SubjectId,
    int? ProjectTypeId,
    int? SchoolCycleId,
    ProjectDevelopmentStatus DevelopmentStatus,
    string? ProblemStatement,
    string? ProposedSolution,
    string? Advisor,
    DateOnly? EstimatedCompletionDate);

public sealed record LinkedStudentCandidate(
    string UserId,
    string FullName,
    string StudentNumber,
    string Career,
    string Grade,
    string Group,
    AccountStatus AccountStatus);

public sealed record ProjectParticipantItem(
    int Id,
    string? UserId,
    string FullName,
    string StudentNumber,
    int CareerId,
    string Career,
    int GradeId,
    string Grade,
    int AcademicGroupId,
    string Group,
    bool IsResponsible,
    bool LinkConfirmed);

public sealed record ProjectHistoryItem(
    long Id,
    ProjectHistoryEventType EventType,
    ProjectAdministrativeStatus? FromStatus,
    ProjectAdministrativeStatus? ToStatus,
    string ActorDisplayName,
    string? Notes,
    int? VersionNumber,
    int? ReviewNumber,
    DateTimeOffset CreatedAtUtc);

public sealed record ProjectReviewRequirementItem(int RequirementId, string Name, bool IsMet);

public sealed record ProjectReviewItem(
    int Id,
    int ReviewNumber,
    int VersionNumber,
    string? AdministratorId,
    string? AdministratorName,
    ProjectReviewDecision Decision,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<ProjectReviewRequirementItem> Requirements);

public sealed record ProjectListItem(
    int Id,
    string Name,
    ProjectAdministrativeStatus Status,
    ProjectDevelopmentStatus DevelopmentStatus,
    string Subject,
    string SchoolCycle,
    int VersionNumber,
    int ReviewNumber,
    DateTimeOffset UpdatedAtUtc,
    bool IsLinkedParticipant = false);

public sealed record StudentProjectDashboardSummary(
    int ActiveProjects,
    int UnderReview,
    int ChangesRequested,
    int Approved,
    int TotalProjects,
    int Drafts,
    int Published,
    int UnreadNotifications);

public sealed record ProjectDetails(
    int Id,
    string ResponsibleUserId,
    string ResponsibleName,
    string ResponsibleStudentNumber,
    string Career,
    ProjectAdministrativeStatus Status,
    int VersionId,
    int VersionNumber,
    string Name,
    int? SubjectId,
    string Subject,
    int? ProjectTypeId,
    string ProjectType,
    int? SchoolCycleId,
    string SchoolCycle,
    ProjectDevelopmentStatus DevelopmentStatus,
    string ProblemStatement,
    string ProposedSolution,
    string? Advisor,
    DateOnly? EstimatedCompletionDate,
    string? AssignedAdministratorId,
    string? AssignedAdministratorName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    IReadOnlyList<ProjectParticipantItem> Participants,
    IReadOnlyList<ProjectReviewItem> Reviews,
    IReadOnlyList<ProjectHistoryItem> History)
{
    public bool IsResponsible { get; init; } = true;
}

public sealed record AdminProjectFilter(
    ProjectAdministrativeStatus? Status,
    int? CareerId,
    int? SubjectId,
    int? SchoolCycleId,
    string? Search);

public sealed record AdminProjectListItem(
    int Id,
    string Name,
    string ResponsibleName,
    string ResponsibleStudentNumber,
    string Career,
    string Subject,
    string SchoolCycle,
    ProjectAdministrativeStatus Status,
    DateTimeOffset? SubmittedAtUtc,
    string? AssignedAdministratorName,
    int VersionNumber,
    int ReviewNumber);

public sealed record ProjectDecisionData(
    string? Notes,
    IReadOnlyCollection<int> MissingRequirementIds);
