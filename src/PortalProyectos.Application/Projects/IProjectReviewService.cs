using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Common;
using PortalProyectos.Domain.Projects;

namespace PortalProyectos.Application.Projects;

public interface IProjectReviewService
{
    Task<OperationResult> FinishAsync(int projectId, string administratorId, CancellationToken cancellationToken = default);
    Task<OperationResult> ReopenAsync(int projectId, string administratorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminProjectListItem>> GetProjectsAsync(AdminProjectFilter filter, CancellationToken cancellationToken = default);
    Task<ProjectDetails?> GetProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewRequirementItem>> GetActiveRequirementsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> TakeReviewAsync(int projectId, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> ApproveAsync(int projectId, string administratorId, string administratorName, string? notes, CancellationToken cancellationToken = default);
    Task<OperationResult> RejectAsync(int projectId, string administratorId, string administratorName, string? notes, CancellationToken cancellationToken = default);
    Task<OperationResult> RequestChangesAsync(int projectId, string administratorId, string administratorName, ProjectDecisionData data, CancellationToken cancellationToken = default);
}
