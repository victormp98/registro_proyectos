namespace PortalProyectos.Application.Catalogs;

public interface IRegistrationCatalogService
{
    Task<RegistrationCatalogs> GetAsync(CancellationToken cancellationToken = default);
}
