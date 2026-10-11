using PortalProyectos.Application.Common;

namespace PortalProyectos.Application.Catalogs;

public interface ICareerService
{
    Task<IReadOnlyList<CareerItem>> GetAllAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<CareerItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OperationResult> CreateAsync(string name, string? code, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(int id, string name, string? code, bool isActive, string administratorId, string administratorName, CancellationToken cancellationToken = default);
}
