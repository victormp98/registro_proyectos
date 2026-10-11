using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Common;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Catalogs;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class CareerService(PortalDbContext dbContext) : ICareerService
{
    public async Task<IReadOnlyList<CareerItem>> GetAllAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Careers.AsNoTracking();
        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        var items = await query.OrderBy(x => x.Name)
            .Select(x => new CareerItem(x.Id, x.Name, x.Code, x.IsActive))
            .ToListAsync(cancellationToken);
        var usage = await GetUsageCountsAsync(cancellationToken);
        return items.Select(x => x with { UsageCount = usage.GetValueOrDefault(x.Id) }).ToArray();
    }

    public async Task<CareerItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.Careers.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CareerItem(x.Id, x.Name, x.Code, x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null) return null;
        var usage = await GetUsageCountsAsync(cancellationToken);
        return item with { UsageCount = usage.GetValueOrDefault(id) };
    }

    public async Task<OperationResult> CreateAsync(
        string name,
        string? code,
        string administratorId,
        string administratorName,
        CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        code = NormalizeCode(code);

        if (await dbContext.Careers.AnyAsync(x => x.Name == name || (code != null && x.Code == code), cancellationToken))
        {
            return OperationResult.Failure("Ya existe una carrera con ese nombre o clave.");
        }

        var career = new Career { Name = name, Code = code };
        dbContext.Careers.Add(career);
        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(administratorId, administratorName, "Catálogo.CarreraCreada", career, $"Se creó la carrera {career.Name}.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success("Carrera creada correctamente.");
    }

    public async Task<OperationResult> UpdateAsync(
        int id,
        string name,
        string? code,
        bool isActive,
        string administratorId,
        string administratorName,
        CancellationToken cancellationToken = default)
    {
        var career = await dbContext.Careers.FindAsync([id], cancellationToken);
        if (career is null)
        {
            return OperationResult.Failure("No se encontró la carrera.");
        }

        name = name.Trim();
        code = NormalizeCode(code);
        if (await dbContext.Careers.AnyAsync(x => x.Id != id && (x.Name == name || (code != null && x.Code == code)), cancellationToken))
        {
            return OperationResult.Failure("Ya existe una carrera con ese nombre o clave.");
        }

        career.Name = name;
        career.Code = code;
        career.IsActive = isActive;
        career.UpdatedAtUtc = DateTimeOffset.UtcNow;
        AddAudit(administratorId, administratorName, "Catálogo.CarreraModificada", career, $"Se actualizó la carrera {career.Name}.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success("Carrera actualizada correctamente.");
    }

    private static string? NormalizeCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<Dictionary<int, int>> GetUsageCountsAsync(CancellationToken cancellationToken)
    {
        var userCounts = await dbContext.Users.AsNoTracking().Where(x => x.CareerId.HasValue)
            .GroupBy(x => x.CareerId!.Value).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var participantCounts = await dbContext.ProjectParticipants.AsNoTracking().GroupBy(x => x.CareerId)
            .Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var projectCounts = await dbContext.ProjectVersions.AsNoTracking().Where(x => x.SubjectId.HasValue)
            .Join(dbContext.Subjects.AsNoTracking(), x => x.SubjectId, x => (int?)x.Id, (version, subject) => subject.CareerId)
            .GroupBy(x => x).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var ids = userCounts.Keys.Concat(participantCounts.Keys).Concat(projectCounts.Keys).Distinct();
        return ids.ToDictionary(id => id, id => userCounts.GetValueOrDefault(id) + participantCounts.GetValueOrDefault(id) + projectCounts.GetValueOrDefault(id));
    }

    private void AddAudit(string userId, string userName, string action, Career career, string summary) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserDisplayName = userName,
            Action = action,
            EntityType = "Carrera",
            EntityId = career.Id.ToString(),
            Summary = summary
        });
}
