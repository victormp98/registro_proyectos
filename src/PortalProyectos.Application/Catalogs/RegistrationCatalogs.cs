namespace PortalProyectos.Application.Catalogs;

public sealed record CatalogOption(int Id, string Name);

public sealed record RegistrationCatalogs(
    IReadOnlyList<CatalogOption> Careers,
    IReadOnlyList<CatalogOption> Grades,
    IReadOnlyList<CatalogOption> Groups);
