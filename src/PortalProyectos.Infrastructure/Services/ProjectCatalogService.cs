using Microsoft.EntityFrameworkCore;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Common;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Catalogs;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class ProjectCatalogService(PortalDbContext dbContext) : IProjectCatalogService
{
    public async Task<IReadOnlyList<SubjectItem>> GetSubjectsAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Subjects.AsNoTracking();
        if (onlyActive) query = query.Where(x => x.IsActive && x.Career != null && x.Career.IsActive);

        var items = await query.OrderBy(x => x.Career!.Name).ThenBy(x => x.Name)
            .Select(x => new SubjectItem(x.Id, x.Name, x.Code, x.CareerId, x.Career!.Name, x.IsActive))
            .ToListAsync(cancellationToken);
        var usage = await dbContext.ProjectVersions.AsNoTracking().Where(x => x.SubjectId.HasValue)
            .GroupBy(x => x.SubjectId!.Value).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return items.Select(x => x with { UsageCount = usage.GetValueOrDefault(x.Id) }).ToArray();
    }

    public async Task<SubjectItem?> GetSubjectAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.Subjects.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new SubjectItem(x.Id, x.Name, x.Code, x.CareerId, x.Career!.Name, x.IsActive)).SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : item with { UsageCount = await dbContext.ProjectVersions.CountAsync(x => x.SubjectId == id, cancellationToken) };
    }

    public async Task<OperationResult> SaveSubjectAsync(int id, string name, string? code, int careerId, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        code = NormalizeCode(code);
        if (name.Length is < 3 or > 120)
            return OperationResult.Failure("El nombre de la materia debe tener entre 3 y 120 caracteres.");
        if (code?.Length > 24 || code?.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-') == true)
            return OperationResult.Failure("La clave puede tener hasta 24 letras, números o guiones.");
        if (!await dbContext.Careers.AnyAsync(x => x.Id == careerId && x.IsActive, cancellationToken))
            return OperationResult.Failure("Selecciona una carrera activa.");
        if (await dbContext.Subjects.AnyAsync(x => x.Id != id && x.CareerId == careerId && (x.Name == name || (code != null && x.Code == code)), cancellationToken))
            return OperationResult.Failure("Ya existe una materia con ese nombre o clave para la carrera.");

        Subject entity;
        string action;
        if (id == 0)
        {
            entity = new Subject { Name = name, Code = code, CareerId = careerId, IsActive = isActive };
            dbContext.Subjects.Add(entity);
            action = "Catálogo.MateriaCreada";
        }
        else
        {
            var existing = await dbContext.Subjects.FindAsync([id], cancellationToken);
            if (existing is null) return OperationResult.Failure("No se encontró la materia.");
            entity = existing;
            entity.Name = name;
            entity.Code = code;
            entity.CareerId = careerId;
            entity.IsActive = isActive;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            action = "Catálogo.MateriaModificada";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(actorId, actorName, action, "Materia", entity.Id.ToString(), $"Materia {entity.Name}; activa: {entity.IsActive}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success(id == 0 ? "Materia creada correctamente." : "Materia actualizada correctamente.");
    }

    public async Task<IReadOnlyList<SchoolCycleItem>> GetSchoolCyclesAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SchoolCycles.AsNoTracking();
        if (onlyActive) query = query.Where(x => x.IsActive);
        var items = await query.OrderByDescending(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => new SchoolCycleItem(x.Id, x.Name, x.StartDate, x.EndDate, x.IsActive))
            .ToListAsync(cancellationToken);
        var usage = await dbContext.ProjectVersions.AsNoTracking().Where(x => x.SchoolCycleId.HasValue)
            .GroupBy(x => x.SchoolCycleId!.Value).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return items.Select(x => x with { UsageCount = usage.GetValueOrDefault(x.Id) }).ToArray();
    }

    public async Task<SchoolCycleItem?> GetSchoolCycleAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.SchoolCycles.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new SchoolCycleItem(x.Id, x.Name, x.StartDate, x.EndDate, x.IsActive)).SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : item with { UsageCount = await dbContext.ProjectVersions.CountAsync(x => x.SchoolCycleId == id, cancellationToken) };
    }

    public async Task<OperationResult> SaveSchoolCycleAsync(int id, string name, DateOnly? startDate, DateOnly? endDate, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (name.Length is < 3 or > 100)
            return OperationResult.Failure("El nombre del ciclo debe tener entre 3 y 100 caracteres.");
        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
            return OperationResult.Failure("La fecha final no puede ser anterior a la inicial.");
        if (await dbContext.SchoolCycles.AnyAsync(x => x.Id != id && x.Name == name, cancellationToken))
            return OperationResult.Failure("Ya existe un ciclo escolar con ese nombre.");

        SchoolCycle entity;
        string action;
        if (id == 0)
        {
            entity = new SchoolCycle { Name = name, StartDate = startDate, EndDate = endDate, IsActive = isActive };
            dbContext.SchoolCycles.Add(entity);
            action = "Catálogo.CicloCreado";
        }
        else
        {
            var existing = await dbContext.SchoolCycles.FindAsync([id], cancellationToken);
            if (existing is null) return OperationResult.Failure("No se encontró el ciclo escolar.");
            entity = existing;
            entity.Name = name;
            entity.StartDate = startDate;
            entity.EndDate = endDate;
            entity.IsActive = isActive;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            action = "Catálogo.CicloModificado";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(actorId, actorName, action, "CicloEscolar", entity.Id.ToString(), $"Ciclo {entity.Name}; activo: {entity.IsActive}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success(id == 0 ? "Ciclo escolar creado correctamente." : "Ciclo escolar actualizado correctamente.");
    }

    public async Task<IReadOnlyList<ProjectTypeItem>> GetProjectTypesAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProjectTypes.AsNoTracking();
        if (onlyActive) query = query.Where(x => x.IsActive);
        var items = await query.OrderBy(x => x.Name)
            .Select(x => new ProjectTypeItem(x.Id, x.Name, x.Description, x.IsActive))
            .ToListAsync(cancellationToken);
        var usage = await dbContext.ProjectVersions.AsNoTracking().Where(x => x.ProjectTypeId.HasValue)
            .GroupBy(x => x.ProjectTypeId!.Value).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return items.Select(x => x with { UsageCount = usage.GetValueOrDefault(x.Id) }).ToArray();
    }

    public async Task<ProjectTypeItem?> GetProjectTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.ProjectTypes.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ProjectTypeItem(x.Id, x.Name, x.Description, x.IsActive)).SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : item with { UsageCount = await dbContext.ProjectVersions.CountAsync(x => x.ProjectTypeId == id, cancellationToken) };
    }

    public async Task<OperationResult> SaveProjectTypeAsync(int id, string name, string? description, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        description = Normalize(description, 300);
        if (name.Length is < 3 or > 100)
            return OperationResult.Failure("El nombre del tipo debe tener entre 3 y 100 caracteres.");
        if (await dbContext.ProjectTypes.AnyAsync(x => x.Id != id && x.Name == name, cancellationToken))
            return OperationResult.Failure("Ya existe un tipo de proyecto con ese nombre.");

        ProjectType entity;
        string action;
        if (id == 0)
        {
            entity = new ProjectType { Name = name, Description = description, IsActive = isActive };
            dbContext.ProjectTypes.Add(entity);
            action = "Catálogo.TipoProyectoCreado";
        }
        else
        {
            var existing = await dbContext.ProjectTypes.FindAsync([id], cancellationToken);
            if (existing is null) return OperationResult.Failure("No se encontró el tipo de proyecto.");
            entity = existing;
            entity.Name = name;
            entity.Description = description;
            entity.IsActive = isActive;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            action = "Catálogo.TipoProyectoModificado";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(actorId, actorName, action, "TipoProyecto", entity.Id.ToString(), $"Tipo {entity.Name}; activo: {entity.IsActive}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success(id == 0 ? "Tipo de proyecto creado correctamente." : "Tipo de proyecto actualizado correctamente.");
    }

    public async Task<IReadOnlyList<ReviewRequirementItem>> GetRequirementsAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ReviewRequirements.AsNoTracking();
        if (onlyActive) query = query.Where(x => x.IsActive);
        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ReviewRequirementItem(x.Id, x.Name, x.Description, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);
        var usage = await dbContext.ProjectReviewRequirements.AsNoTracking().GroupBy(x => x.ReviewRequirementId)
            .Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return items.Select(x => x with { UsageCount = usage.GetValueOrDefault(x.Id) }).ToArray();
    }

    public async Task<ReviewRequirementItem?> GetRequirementAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.ReviewRequirements.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ReviewRequirementItem(x.Id, x.Name, x.Description, x.SortOrder, x.IsActive)).SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : item with { UsageCount = await dbContext.ProjectReviewRequirements.CountAsync(x => x.ReviewRequirementId == id, cancellationToken) };
    }

    public async Task<OperationResult> SaveRequirementAsync(int id, string name, string? description, int sortOrder, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        description = Normalize(description, 300);
        if (name.Length is < 3 or > 120)
            return OperationResult.Failure("El nombre del requisito debe tener entre 3 y 120 caracteres.");
        if (sortOrder is < 0 or > 999)
            return OperationResult.Failure("El orden debe estar entre 0 y 999.");
        if (await dbContext.ReviewRequirements.AnyAsync(x => x.Id != id && x.Name == name, cancellationToken))
            return OperationResult.Failure("Ya existe un requisito con ese nombre.");

        ReviewRequirement entity;
        string action;
        if (id == 0)
        {
            entity = new ReviewRequirement { Name = name, Description = description, SortOrder = sortOrder, IsActive = isActive };
            dbContext.ReviewRequirements.Add(entity);
            action = "Catálogo.RequisitoCreado";
        }
        else
        {
            var existing = await dbContext.ReviewRequirements.FindAsync([id], cancellationToken);
            if (existing is null) return OperationResult.Failure("No se encontró el requisito.");
            entity = existing;
            entity.Name = name;
            entity.Description = description;
            entity.SortOrder = sortOrder;
            entity.IsActive = isActive;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
            action = "Catálogo.RequisitoModificado";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(actorId, actorName, action, "RequisitoRevision", entity.Id.ToString(), $"Requisito {entity.Name}; activo: {entity.IsActive}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success(id == 0 ? "Requisito creado correctamente." : "Requisito actualizado correctamente.");
    }

    public async Task<ProjectFormCatalogs> GetProjectFormCatalogsAsync(int careerId, CancellationToken cancellationToken = default)
    {
        var subjects = await dbContext.Subjects.AsNoTracking()
            .Where(x => x.IsActive && x.CareerId == careerId && x.Career != null && x.Career.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SubjectItem(x.Id, x.Name, x.Code, x.CareerId, x.Career!.Name, x.IsActive))
            .ToListAsync(cancellationToken);
        var types = await GetProjectTypesAsync(true, cancellationToken);
        var cycles = await GetSchoolCyclesAsync(true, cancellationToken);
        var careers = await dbContext.Careers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new CatalogOption(x.Id, x.Name)).ToListAsync(cancellationToken);
        var grades = await dbContext.Grades.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new CatalogOption(x.Id, x.Name)).ToListAsync(cancellationToken);
        var groups = await dbContext.AcademicGroups.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder)
            .Select(x => new CatalogOption(x.Id, x.Name)).ToListAsync(cancellationToken);
        return new ProjectFormCatalogs(subjects, types, cycles, careers, grades, groups);
    }

    public async Task<IReadOnlyList<AcademicLevelItem>> GetGradesAsync(CancellationToken cancellationToken = default)
    {
        var grades = await dbContext.Grades.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var profileUsage = await dbContext.Users.AsNoTracking().Where(x => x.GradeId.HasValue)
            .GroupBy(x => x.GradeId!.Value).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var participantUsage = await dbContext.ProjectParticipants.AsNoTracking()
            .GroupBy(x => x.GradeId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return grades.Select(x => new AcademicLevelItem(x.Id, x.Name, x.SortOrder, x.IsActive,
            profileUsage.GetValueOrDefault(x.Id) + participantUsage.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<OperationResult> SaveGradeAsync(int id, string name, int sortOrder, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 40 || sortOrder is < 0 or > 999) return OperationResult.Failure("El grado debe tener nombre y orden válidos.");
        if (await dbContext.Grades.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), cancellationToken)) return OperationResult.Failure("Ya existe ese grado.");
        var item = id == 0 ? new Grade { Name = name, SortOrder = sortOrder, IsActive = isActive } : await dbContext.Grades.FindAsync([id], cancellationToken);
        if (item is null) return OperationResult.Failure("No se encontró el grado.");
        item.Name = name; item.SortOrder = sortOrder; item.IsActive = isActive;
        if (id == 0) dbContext.Grades.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(actorId, actorName, id == 0 ? "Catálogo.GradoCreado" : "Catálogo.GradoModificado", "Grado", item.Id.ToString(), $"Grado {name}; activo: {isActive}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success(id == 0 ? "Grado creado." : "Grado actualizado.");
    }

    public async Task<IReadOnlyList<AcademicLevelItem>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await dbContext.AcademicGroups.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var profileUsage = await dbContext.Users.AsNoTracking().Where(x => x.AcademicGroupId.HasValue)
            .GroupBy(x => x.AcademicGroupId!.Value).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var participantUsage = await dbContext.ProjectParticipants.AsNoTracking()
            .GroupBy(x => x.AcademicGroupId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return groups.Select(x => new AcademicLevelItem(x.Id, x.Name, x.SortOrder, x.IsActive,
            profileUsage.GetValueOrDefault(x.Id) + participantUsage.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<OperationResult> SaveGroupAsync(int id, string name, int sortOrder, bool isActive, string actorId, string actorName, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 40 || sortOrder is < 0 or > 999) return OperationResult.Failure("El grupo debe tener nombre y orden válidos.");
        if (await dbContext.AcademicGroups.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), cancellationToken)) return OperationResult.Failure("Ya existe ese grupo.");
        var item = id == 0 ? new AcademicGroup { Name = name, SortOrder = sortOrder, IsActive = isActive } : await dbContext.AcademicGroups.FindAsync([id], cancellationToken);
        if (item is null) return OperationResult.Failure("No se encontró el grupo.");
        item.Name = name; item.SortOrder = sortOrder; item.IsActive = isActive;
        if (id == 0) dbContext.AcademicGroups.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(actorId, actorName, id == 0 ? "Catálogo.GrupoCreado" : "Catálogo.GrupoModificado", "Grupo", item.Id.ToString(), $"Grupo {name}; activo: {isActive}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success(id == 0 ? "Grupo creado." : "Grupo actualizado.");
    }

    private void AddAudit(string actorId, string actorName, string action, string entityType, string entityId, string summary) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            UserDisplayName = actorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary
        });

    private static string? NormalizeCode(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
}
