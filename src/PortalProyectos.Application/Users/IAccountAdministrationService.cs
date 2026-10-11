using PortalProyectos.Application.Common;
using PortalProyectos.Domain.Users;

namespace PortalProyectos.Application.Users;

public interface IAccountAdministrationService
{
    Task<OperationResult> ChangeAdministratorStatusAsync(string userId, AccountStatus newStatus,
        string administratorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountReviewItem>> GetStudentsAsync(
        AccountStatus? status,
        string? search,
        CancellationToken cancellationToken = default);

    Task<AdminDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> ChangeStatusAsync(
        string userId,
        AccountStatus newStatus,
        string? reason,
        string administratorId,
        string administratorName,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UnblockAsync(
        string userId,
        string administratorId,
        string administratorName,
        CancellationToken cancellationToken = default);
}
