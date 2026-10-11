namespace PortalProyectos.Application.Catalogs;

public sealed record SubjectItem(int Id, string Name, string? Code, int CareerId, string Career, bool IsActive, int UsageCount = 0);

public sealed record SchoolCycleItem(
    int Id,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive,
    int UsageCount = 0);

public sealed record ProjectTypeItem(int Id, string Name, string? Description, bool IsActive, int UsageCount = 0);

public sealed record ReviewRequirementItem(
    int Id,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    int UsageCount = 0);

public sealed record AcademicLevelItem(int Id, string Name, int SortOrder, bool IsActive, int UsageCount);

public sealed record ProjectFormCatalogs(
    IReadOnlyList<SubjectItem> Subjects,
    IReadOnlyList<ProjectTypeItem> ProjectTypes,
    IReadOnlyList<SchoolCycleItem> SchoolCycles,
    IReadOnlyList<CatalogOption> Careers,
    IReadOnlyList<CatalogOption> Grades,
    IReadOnlyList<CatalogOption> Groups);
