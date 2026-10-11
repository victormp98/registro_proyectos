using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class RegistrationCatalogService(PortalDbContext dbContext) : IRegistrationCatalogService
{
    public async Task<RegistrationCatalogs> GetAsync(CancellationToken cancellationToken = default)
    {
        var careers = await dbContext.Careers.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CatalogOption(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        var grades = await dbContext.Grades.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new CatalogOption(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        var groups = await dbContext.AcademicGroups.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new CatalogOption(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        return new RegistrationCatalogs(careers, grades, groups);
    }
}
