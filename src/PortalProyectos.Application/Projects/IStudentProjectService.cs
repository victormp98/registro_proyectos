using PortalProyectos.Application.Common;
using PortalProyectos.Domain.Projects;

namespace PortalProyectos.Application.Projects;

public interface IStudentProjectService
{
    ProjectLimits Limits { get; }
    Task<StudentProjectDashboardSummary> GetDashboardSummaryAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectListItem>> GetProjectsAsync(string userId, ProjectAdministrativeStatus? status = null, CancellationToken cancellationToken = default);
    Task<ProjectDetails?> GetProjectAsync(string userId, int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectListItem>> GetLinkedProjectsAsync(string userId, ProjectAdministrativeStatus? status = null, CancellationToken cancellationToken = default);
    Task<ProjectDetails?> GetLinkedProjectAsync(string userId, int projectId, CancellationToken cancellationToken = default);
    Task<OperationResult<int>> CreateDraftAsync(string userId, string actorName, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateDraftAsync(string userId, int projectId, ProjectDraftData data, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteDraftAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default);
    Task<OperationResult<LinkedStudentCandidate>> FindStudentAsync(string userId, int projectId, string studentNumber, CancellationToken cancellationToken = default);
    Task<OperationResult> AddLinkedParticipantAsync(string userId, string actorName, int projectId, string linkedUserId, CancellationToken cancellationToken = default);
    Task<OperationResult> RemoveParticipantAsync(string userId, string actorName, int projectId, int participantId, CancellationToken cancellationToken = default);
    Task<OperationResult> SubmitAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default);
    Task<OperationResult> CancelSubmissionAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default);
}
