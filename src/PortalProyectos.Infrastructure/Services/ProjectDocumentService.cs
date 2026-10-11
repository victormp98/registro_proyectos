using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Files;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Configuration;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class ProjectDocumentService(
    PortalDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IFileStorageService fileStorage,
    IOptions<StorageOptions> options,
    ILogger<ProjectDocumentService> logger) : IProjectDocumentService
{
    private const string PdfMime = "application/pdf";

    public async Task<ProjectDocumentItem?> GetForProjectOwnerAsync(string userId, int projectId, CancellationToken cancellationToken = default)
    {
        if (await GetApprovedStudentAsync(userId) is null) return null;
        var versionId = await dbContext.Projects.AsNoTracking().Where(x => x.Id == projectId && x.IsActive && x.ResponsibleUserId == userId)
            .Select(x => x.CurrentVersionId).SingleOrDefaultAsync(cancellationToken);
        return versionId is null ? null : await FindActiveDocumentAsync(projectId, versionId.Value, cancellationToken);
    }

    public async Task<ProjectDocumentItem?> GetForLinkedParticipantAsync(string userId, int projectId, CancellationToken cancellationToken = default)
    {
        if (await GetApprovedStudentAsync(userId) is null) return null;
        var versionId = await dbContext.ProjectParticipants.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.UserId == userId && x.LinkConfirmed
                && x.Project != null && x.Project.IsActive && x.Project.ResponsibleUserId != userId)
            .Select(x => x.Project!.CurrentVersionId)
            .SingleOrDefaultAsync(cancellationToken);
        return versionId is null ? null : await FindActiveDocumentAsync(projectId, versionId.Value, cancellationToken);
    }

    public async Task<ProjectDocumentItem?> GetForAdministratorAsync(string administratorId, int projectId, CancellationToken cancellationToken = default)
    {
        var administrator = await userManager.FindByIdAsync(administratorId);
        if (administrator is null || !await IsAdministratorAsync(administrator)) return null;
        var project = await dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId && x.IsActive
            && x.CurrentStatus != ProjectAdministrativeStatus.Draft && x.CurrentVersionId != null, cancellationToken);
        return project?.CurrentVersionId is null ? null : await FindActiveDocumentAsync(projectId, project.CurrentVersionId.Value, cancellationToken);
    }

    public async Task<OperationResult> UploadAsync(string userId, string actorName, int projectId, string originalFileName, string contentType, long length, Stream content, CancellationToken cancellationToken = default)
    {
        var user = await GetApprovedStudentAsync(userId);
        if (user is null) return OperationResult.Failure("Solo un alumno con cuenta aprobada puede administrar documentos.");
        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && x.IsActive && x.ResponsibleUserId == userId, cancellationToken);
        if (project?.CurrentVersionId is null || project.CurrentStatus is not (ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested or ProjectAdministrativeStatus.Approved))
            return OperationResult.Failure("El documento solo puede modificarse en borrador, durante correcciones o después de aprobarse el proyecto.");
        if (project.CurrentStatus is ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested)
        {
            var hasSavedBasics = await dbContext.ProjectVersions.AnyAsync(x => x.Id == project.CurrentVersionId.Value
                && !string.IsNullOrWhiteSpace(x.Name) && x.SubjectId != null && x.ProjectTypeId != null && x.SchoolCycleId != null, cancellationToken);
            if (!hasSavedBasics)
                return OperationResult.Failure("Guarda primero el proyecto como borrador para agregar documentación.");
        }
        if (!HasPdfExtension(originalFileName) || !string.Equals(contentType, PdfMime, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Failure("Solo se aceptan archivos PDF.");
        var maxBytes = Math.Clamp(options.Value.MaxProjectPdfBytes, 1, 100 * 1024 * 1024);
        if (length <= 0 || length > maxBytes) return OperationResult.Failure($"El PDF debe pesar como máximo {maxBytes / (1024 * 1024)} MB.");

        await using var buffer = new MemoryStream((int)length);
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != length || buffer.Length > maxBytes) return OperationResult.Failure("El tamaño recibido no coincide o supera el límite permitido.");
        var header = buffer.ToArray().Take(5).ToArray();
        if (header.Length < 5 || !header.SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })) return OperationResult.Failure("El archivo no contiene una cabecera PDF válida.");

        var displayName = SafeOriginalName(originalFileName);
        buffer.Position = 0;
        var relativeDirectory = $"projects/{projectId}/versions/{project.CurrentVersionId.Value}/documents";
        StoredFileDescriptor stored;
        try
        {
            stored = await fileStorage.SaveAsync(buffer, relativeDirectory, ".pdf", cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "No fue posible escribir el PDF del proyecto {ProjectId}.", projectId);
            return OperationResult.Failure("No se pudo guardar el PDF en el almacenamiento del servidor.");
        }
        List<MediaFile> existing = [];
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            existing = await dbContext.MediaFiles.Where(x => x.ProjectVersionId == project.CurrentVersionId.Value
                    && x.FileType == StoredFileType.ProjectDocument && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var old in existing) old.IsDeleted = true;
            var media = new MediaFile
            {
                ProjectId = projectId,
                ProjectVersionId = project.CurrentVersionId.Value,
                OriginalFileName = displayName,
                StoredFileName = stored.StoredFileName,
                RelativePath = stored.RelativePath,
                MimeType = PdfMime,
                SizeBytes = stored.SizeBytes,
                FileType = StoredFileType.ProjectDocument,
                UploadedByUserId = userId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.MediaFiles.Add(media);
            var postApproval = project.CurrentStatus == ProjectAdministrativeStatus.Approved;
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                UserDisplayName = actorName,
                Action = postApproval
                    ? existing.Count == 0 ? "Archivo.PDFAgregadoPostAprobacion" : "Archivo.PDFReemplazadoPostAprobacion"
                    : existing.Count == 0 ? "Archivo.PDFSubido" : "Archivo.PDFReemplazado",
                EntityType = "Proyecto",
                EntityId = projectId.ToString(),
                Summary = $"Documento de versión {project.CurrentVersionId} {(existing.Count == 0 ? "agregado" : "reemplazado")}; {stored.SizeBytes} bytes."
            });
            project.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            try { await fileStorage.DeleteAsync(stored.RelativePath, CancellationToken.None); }
            catch (Exception cleanupException) { logger.LogError(cleanupException, "No se pudo retirar un archivo sin metadatos del proyecto {ProjectId}.", projectId); }
            dbContext.ChangeTracker.Clear();
            logger.LogError(exception, "No se pudo asociar en SQLite el PDF del proyecto {ProjectId}; se limpió el archivo recién escrito.", projectId);
            if (exception is OperationCanceledException) throw;
            return OperationResult.Failure("No se pudo registrar el documento del proyecto. Intenta nuevamente.");
        }

        foreach (var old in existing)
            await DeletePhysicalAfterCommitAsync(old, projectId, cancellationToken);
        return OperationResult.Success(existing.Count == 0 ? "PDF guardado correctamente." : "El PDF fue reemplazado correctamente.");
    }

    public async Task<OperationResult> DeleteAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default)
    {
        if (await GetApprovedStudentAsync(userId) is null) return OperationResult.Failure("Tu cuenta debe estar aprobada.");
        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == projectId && x.IsActive && x.ResponsibleUserId == userId, cancellationToken);
        if (project?.CurrentVersionId is null || project.CurrentStatus is not (ProjectAdministrativeStatus.Draft or ProjectAdministrativeStatus.ChangesRequested))
            return OperationResult.Failure("El documento no puede eliminarse en el estado actual del proyecto.");
        var hasSavedBasics = await dbContext.ProjectVersions.AnyAsync(x => x.Id == project.CurrentVersionId.Value
            && !string.IsNullOrWhiteSpace(x.Name) && x.SubjectId != null && x.ProjectTypeId != null && x.SchoolCycleId != null, cancellationToken);
        if (!hasSavedBasics)
            return OperationResult.Failure("Guarda primero el proyecto como borrador para administrar documentación.");
        var media = await dbContext.MediaFiles.SingleOrDefaultAsync(x => x.ProjectVersionId == project.CurrentVersionId.Value
            && x.FileType == StoredFileType.ProjectDocument && !x.IsDeleted, cancellationToken);
        if (media is null) return OperationResult.Failure("No hay un PDF activo para eliminar.");

        media.IsDeleted = true;
        project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserDisplayName = actorName,
            Action = "Archivo.PDFEliminado",
            EntityType = "Proyecto",
            EntityId = projectId.ToString(),
            Summary = $"Se eliminó el documento {media.OriginalFileName} de la versión {media.ProjectVersionId}."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await DeletePhysicalAfterCommitAsync(media, projectId, cancellationToken);
        return OperationResult.Success("El PDF fue eliminado del almacenamiento y del proyecto.");
    }

    public async Task<(Stream Content, ProjectDocumentItem Document)?> OpenForProjectOwnerAsync(string userId, int projectId, CancellationToken cancellationToken = default)
    {
        var document = await GetForProjectOwnerAsync(userId, projectId, cancellationToken);
        return document is null ? null : await OpenAsync(userId, projectId, document, cancellationToken);
    }

    public async Task<(Stream Content, ProjectDocumentItem Document)?> OpenForLinkedParticipantAsync(string userId, int projectId, CancellationToken cancellationToken = default)
    {
        var document = await GetForLinkedParticipantAsync(userId, projectId, cancellationToken);
        return document is null ? null : await OpenAsync(userId, projectId, document, cancellationToken);
    }

    public async Task<(Stream Content, ProjectDocumentItem Document)?> OpenForAdministratorAsync(string administratorId, int projectId, CancellationToken cancellationToken = default)
    {
        var document = await GetForAdministratorAsync(administratorId, projectId, cancellationToken);
        return document is null ? null : await OpenAsync(administratorId, projectId, document, cancellationToken);
    }

    private async Task<(Stream Content, ProjectDocumentItem Document)?> OpenAsync(string actorId, int projectId, ProjectDocumentItem document, CancellationToken cancellationToken)
    {
        var entity = await dbContext.MediaFiles.SingleOrDefaultAsync(x => x.Id == document.Id && x.ProjectId == projectId && !x.IsDeleted, cancellationToken);
        if (entity is null) return null;
        var stream = await fileStorage.OpenReadAsync(entity.RelativePath, cancellationToken);
        if (stream is not null) return (stream, document);

        logger.LogError("Falta el archivo físico {RelativePath} asociado al documento {MediaFileId}.", entity.RelativePath, entity.Id);
        entity.IsDeleted = true;
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            UserDisplayName = "Sistema",
            Action = "Archivo.StorageInconsistente",
            EntityType = "Archivo",
            EntityId = entity.Id.ToString(),
            Summary = "El registro de metadatos existía, pero no se encontró el archivo físico. Se marcó como eliminado."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task DeletePhysicalAfterCommitAsync(MediaFile media, int projectId, CancellationToken cancellationToken)
    {
        try
        {
            if (!await fileStorage.DeleteAsync(media.RelativePath, cancellationToken))
            {
                logger.LogWarning("El archivo {MediaFileId} ya no existía físicamente; se conservará la operación con metadatos históricos.", media.Id);
                await LogStorageIncidentAsync(media, projectId, "El metadato existía, pero el archivo físico faltaba al intentar eliminarlo.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "No fue posible borrar el archivo físico {MediaFileId} del proyecto {ProjectId}.", media.Id, projectId);
            await LogStorageIncidentAsync(media, projectId, "El servidor no pudo borrar el archivo físico; revisar permisos y limpieza del storage.");
        }
    }

    private async Task LogStorageIncidentAsync(MediaFile media, int projectId, string summary)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = media.UploadedByUserId,
            UserDisplayName = "Sistema",
            Action = "Archivo.StorageInconsistente",
            EntityType = "Archivo",
            EntityId = media.Id.ToString(),
            Summary = $"Proyecto {projectId}: {summary}"
        });
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<ProjectDocumentItem?> FindActiveDocumentAsync(int projectId, int versionId, CancellationToken cancellationToken) =>
        await dbContext.MediaFiles.AsNoTracking().Where(x => x.ProjectId == projectId && x.ProjectVersionId == versionId
                && x.FileType == StoredFileType.ProjectDocument && !x.IsDeleted)
            .Select(x => new ProjectDocumentItem(x.Id, x.OriginalFileName, x.MimeType, x.SizeBytes, x.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ApplicationUser?> GetApprovedStudentAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is not null && user.AccountStatus == AccountStatus.Approved && await userManager.IsInRoleAsync(user, SystemRoles.Student) ? user : null;
    }

    private async Task<bool> IsAdministratorAsync(ApplicationUser user) =>
        await userManager.IsInRoleAsync(user, SystemRoles.Administrator) || await userManager.IsInRoleAsync(user, SystemRoles.SuperAdministrator);

    private static bool HasPdfExtension(string fileName) => string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);

    private static string SafeOriginalName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c is not '/' and not '\\').Take(255).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "documento.pdf" : cleaned;
    }
}
