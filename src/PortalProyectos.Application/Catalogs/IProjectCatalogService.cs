using PortalProyectos.Application.Common;

namespace PortalProyectos.Application.Catalogs;

public interface IProjectCatalogService
{
    Task<IReadOnlyList<SubjectItem>> GetSubjectsAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<SubjectItem?> GetSubjectAsync(int id, CancellationToken cancellationToken = default);
    Task<OperationResult> SaveSubjectAsync(int id, string name, string? code, int careerId, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolCycleItem>> GetSchoolCyclesAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<SchoolCycleItem?> GetSchoolCycleAsync(int id, CancellationToken cancellationToken = default);
    Task<OperationResult> SaveSchoolCycleAsync(int id, string name, DateOnly? startDate, DateOnly? endDate, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectTypeItem>> GetProjectTypesAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<ProjectTypeItem?> GetProjectTypeAsync(int id, CancellationToken cancellationToken = default);
    Task<OperationResult> SaveProjectTypeAsync(int id, string name, string? description, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewRequirementItem>> GetRequirementsAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<ReviewRequirementItem?> GetRequirementAsync(int id, CancellationToken cancellationToken = default);
    Task<OperationResult> SaveRequirementAsync(int id, string name, string? description, int sortOrder, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default);

    Task<ProjectFormCatalogs> GetProjectFormCatalogsAsync(int careerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicLevelItem>> GetGradesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> SaveGradeAsync(int id, string name, int sortOrder, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicLevelItem>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> SaveGroupAsync(int id, string name, int sortOrder, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default);
}
