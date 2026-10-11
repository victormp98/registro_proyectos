using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Application.Projects;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Files;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class ProjectPublicationService(
    PortalDbContext db,
    UserManager<ApplicationUser> users,
    IFileStorageService storage,
    INotificationService notifications,
    ILogger<ProjectPublicationService> logger) : IProjectPublicationService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    public async Task<PublicationPage> GetPublicPageAsync(PublicProjectFilter filter, int page, string? userId, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        const int pageSize = 12;
        var query = PublicQuery();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x => x.Version.Name.Contains(term));
        }
        if (filter.CareerId.HasValue) query = query.Where(x => x.Version.Subject != null && x.Version.Subject.CareerId == filter.CareerId);
        if (filter.SubjectId.HasValue) query = query.Where(x => x.Version.SubjectId == filter.SubjectId);
        if (filter.TypeId.HasValue) query = query.Where(x => x.Version.ProjectTypeId == filter.TypeId);
        if (filter.DevelopmentStatus.HasValue) query = query.Where(x => x.DevelopmentVersion.DevelopmentStatus == filter.DevelopmentStatus);
        if (filter.CycleId.HasValue) query = query.Where(x => x.Version.SchoolCycleId == filter.CycleId);
        var total = await query.CountAsync(cancellationToken);
        query = filter.Sort switch
        {
            "oldest" => query.OrderBy(x => x.Publication.Id),
            "likes" => query.OrderByDescending(x => x.Project.ProjectLikes.Count).ThenByDescending(x => x.Publication.Id),
            _ => query.OrderByDescending(x => x.Publication.IsFeatured).ThenByDescending(x => x.Publication.Id)
        };
        var projects = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new
        {
            x.Project.Id, VersionId = x.Version.Id, x.Version.Name,
            Career = x.Version.Subject != null && x.Version.Subject.Career != null ? x.Version.Subject.Career.Name : "Sin carrera",
            Subject = x.Version.Subject != null ? x.Version.Subject.Name : "Sin materia",
            Type = x.Version.ProjectType != null ? x.Version.ProjectType.Name : "Sin tipo",
            Cycle = x.Version.SchoolCycle != null ? x.Version.SchoolCycle.Name : "Sin ciclo",
            x.DevelopmentVersion.DevelopmentStatus, x.Version.ProblemStatement,
            x.Publication.IsFeatured, PublishedAt = x.Publication.PublishedAtUtc!.Value,
            Likes = x.Project.ProjectLikes.Count,
            IsLiked = userId != null && x.Project.ProjectLikes.Any(l => l.UserId == userId),
            Cover = db.MediaFiles.Where(m => m.ProjectId == x.Project.Id && m.FileType == StoredFileType.ProjectImage && !m.IsDeleted && m.IsCover).Select(m => (long?)m.Id).FirstOrDefault()
        }).ToListAsync(cancellationToken);
        var careerOptions = await db.Careers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new PublicationFilterOption(x.Id, x.Name)).ToListAsync(cancellationToken);
        var subjects = await db.Subjects.AsNoTracking().Where(x => x.IsActive && (filter.CareerId == null || x.CareerId == filter.CareerId)).OrderBy(x => x.Name).Select(x => new PublicationFilterOption(x.Id, x.Name)).ToListAsync(cancellationToken);
        var types = await db.ProjectTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new PublicationFilterOption(x.Id, x.Name)).ToListAsync(cancellationToken);
        var cycles = await db.SchoolCycles.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.Id).Select(x => new PublicationFilterOption(x.Id, x.Name)).ToListAsync(cancellationToken);
        return new PublicationPage(projects.Select(x => Card(x.Id, x.VersionId, x.Name, x.Career, x.Subject, x.Type, x.Cycle, x.DevelopmentStatus, x.ProblemStatement, x.Cover, x.Likes, x.IsLiked, x.IsFeatured, x.PublishedAt)).ToArray(), page, pageSize, total, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)), careerOptions, subjects, types, cycles);
    }

    public async Task<PublicProjectDetails?> GetPublicDetailsAsync(int projectId, string? userId, CancellationToken cancellationToken = default)
    {
        var row = await PublicQuery().Where(x => x.Project.Id == projectId).Select(x => new
        {
            x.Project.Id, VersionId = x.Version.Id, x.Version.Name,
            Career = x.Version.Subject != null && x.Version.Subject.Career != null ? x.Version.Subject.Career.Name : "Sin carrera",
            Subject = x.Version.Subject != null ? x.Version.Subject.Name : "Sin materia",
            Type = x.Version.ProjectType != null ? x.Version.ProjectType.Name : "Sin tipo",
            Cycle = x.Version.SchoolCycle != null ? x.Version.SchoolCycle.Name : "Sin ciclo",
            x.DevelopmentVersion.DevelopmentStatus, x.Version.ProblemStatement, x.Version.ProposedSolution, x.Version.Advisor,
            x.Publication.IsFeatured, PublishedAt = x.Publication.PublishedAtUtc!.Value,
            Likes = x.Project.ProjectLikes.Count,
            IsLiked = userId != null && x.Project.ProjectLikes.Any(l => l.UserId == userId)
        }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var images = await db.MediaFiles.AsNoTracking().Where(x => x.ProjectId == projectId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted)
            .OrderByDescending(x => x.IsCover).ThenBy(x => x.SortOrder).Select(x => new PublicImage($"/media/projects/{x.Id}", x.AltText, x.IsCover, x.SortOrder)).ToListAsync(cancellationToken);
        var publicSnapshot = await db.ProjectPublications.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => x.ParticipantsSnapshot).SingleAsync(cancellationToken);
        var participants = string.IsNullOrWhiteSpace(publicSnapshot)
            ? await (from p in db.ProjectParticipants.AsNoTracking()
                     join c in db.Careers.AsNoTracking() on p.CareerId equals c.Id into careerJoin
                     from c in careerJoin.DefaultIfEmpty()
                     where p.ProjectId == projectId orderby p.IsResponsible descending, p.FullName
                     select new PublicParticipant(p.FullName, c == null ? null : c.Name)).ToListAsync(cancellationToken)
            : JsonSerializer.Deserialize<List<PublicParticipant>>(publicSnapshot) ?? [];
        var cover = images.FirstOrDefault(x => x.IsCover)?.Url;
        var card = Card(row.Id, row.VersionId, row.Name, row.Career, row.Subject, row.Type, row.Cycle, row.DevelopmentStatus, row.ProblemStatement, null, row.Likes, row.IsLiked, row.IsFeatured, row.PublishedAt) with { CoverUrl = cover };
        return new PublicProjectDetails(card, row.ProblemStatement, row.ProposedSolution, row.Advisor, participants, images);
    }

    public async Task<(Stream Content, string MimeType, string? AltText)?> OpenPublicImageAsync(long mediaId, CancellationToken cancellationToken = default)
    {
        var media = await db.MediaFiles.AsNoTracking().Where(x => x.Id == mediaId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted && x.ProjectId != null
            && db.ProjectPublications.Any(p => p.ProjectId == x.ProjectId && p.IsPublished && p.Project!.IsActive))
            .Select(x => new { x.RelativePath, x.MimeType, x.AltText }).SingleOrDefaultAsync(cancellationToken);
        if (media is null) return null;
        var stream = await storage.OpenReadAsync(media.RelativePath, cancellationToken);
        if (stream is null) { logger.LogWarning("Falta archivo de imagen pública {MediaId}.", mediaId); return null; }
        return (stream, media.MimeType, media.AltText);
    }

    public async Task<(Stream Content, string MimeType, string? AltText)?> OpenAdminImageAsync(string administratorId, long mediaId, CancellationToken cancellationToken = default)
    {
        var administrator = await users.FindByIdAsync(administratorId);
        if (administrator is null || (!await users.IsInRoleAsync(administrator, SystemRoles.Administrator) && !await users.IsInRoleAsync(administrator, SystemRoles.SuperAdministrator))) return null;
        var relativePath = await db.MediaFiles.AsNoTracking().Where(x => x.Id == mediaId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted)
            .Select(x => new { x.RelativePath, x.MimeType, x.AltText }).SingleOrDefaultAsync(cancellationToken);
        if (relativePath is null) return null;
        var stream = await storage.OpenReadAsync(relativePath.RelativePath, cancellationToken);
        return stream is null ? null : (stream, relativePath.MimeType, relativePath.AltText);
    }

    public async Task<IReadOnlyList<PublicationAdminItem>> GetAdminItemsAsync(PublicationAdminFilter filter, CancellationToken cancellationToken = default)
    {
        var query = from p in db.Projects.AsNoTracking()
                    join u in db.Users.AsNoTracking() on p.ResponsibleUserId equals u.Id
                    join publication in db.ProjectPublications.AsNoTracking() on p.Id equals publication.ProjectId into publications
                    from publication in publications.DefaultIfEmpty()
                    join version in db.ProjectVersions.AsNoTracking() on (publication == null ? p.LatestApprovedVersionId : publication.PublishedProjectVersionId) equals version.Id into versions
                    from version in versions.DefaultIfEmpty()
                    where p.IsActive && p.LatestApprovedVersionId != null
                    select new { Project = p, User = u, Publication = publication, Version = version };
        if (filter.CareerId.HasValue) query = query.Where(x => x.Version != null && x.Version.Subject != null && x.Version.Subject.CareerId == filter.CareerId);
        if (filter.CycleId.HasValue) query = query.Where(x => x.Version != null && x.Version.SchoolCycleId == filter.CycleId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Version != null && x.Version.Name.Contains(filter.Search.Trim()) || x.User.FullName.Contains(filter.Search.Trim()));
        query = filter.State switch
        {
            "published" => query.Where(x => x.Publication != null && x.Publication.IsPublished),
            "hidden" => query.Where(x => x.Publication != null && !x.Publication.IsPublished),
            "featured" => query.Where(x => x.Publication != null && x.Publication.IsPublished && x.Publication.IsFeatured),
            "unpublished" => query.Where(x => x.Publication == null),
            _ => query
        };
        return await query.OrderByDescending(x => x.Publication != null && x.Publication.IsFeatured).ThenByDescending(x => x.Publication == null ? 0 : x.Publication.Id)
            .Select(x => new PublicationAdminItem(x.Project.Id, x.Version == null ? "Sin versión aprobada" : x.Version.Name, x.User.FullName,
                x.Version != null && x.Version.Subject != null && x.Version.Subject.Career != null ? x.Version.Subject.Career.Name : "Sin carrera",
                x.Version != null && x.Version.SchoolCycle != null ? x.Version.SchoolCycle.Name : "Sin ciclo", x.Project.CurrentStatus,
                x.Publication != null && x.Publication.IsPublished, x.Publication != null && x.Publication.IsFeatured,
                x.Publication == null ? null : (int?)x.Publication.PublishedProjectVersionId,
                x.Version == null ? null : (int?)x.Version.VersionNumber,
                x.Publication == null ? null : x.Publication.PublishedAtUtc, x.Project.ProjectLikes.Count)).ToListAsync(cancellationToken);
    }

    public async Task<PublicationAdminDetails?> GetAdminDetailsAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.AsNoTracking().Where(x => x.Id == projectId && x.IsActive && x.LatestApprovedVersionId != null)
            .Select(x => new { x.Id, x.ResponsibleUserId, x.CurrentStatus, x.LatestApprovedVersionId }).SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;
        var responsible = await users.FindByIdAsync(project.ResponsibleUserId);
        var publication = await db.ProjectPublications.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);
        var developmentStatus = await db.ProjectVersions.AsNoTracking().Where(x => x.Id == project.LatestApprovedVersionId)
            .Select(x => x.DevelopmentStatus).SingleAsync(cancellationToken);
        var versions = await (from v in db.ProjectVersions.AsNoTracking()
                              join r in db.ProjectReviews.AsNoTracking() on v.Id equals r.ProjectVersionId
                              where v.ProjectId == projectId && r.Decision == ProjectReviewDecision.Approved
                              orderby v.VersionNumber descending
                              select new ApprovedVersionOption(v.Id, v.VersionNumber, v.Name,
                                  v.Subject != null && v.Subject.Career != null ? v.Subject.Career.Name : "Sin carrera",
                                  v.Subject != null ? v.Subject.Name : "Sin materia",
                                  v.SchoolCycle != null ? v.SchoolCycle.Name : "Sin ciclo",
                                  v.ProjectType != null ? v.ProjectType.Name : "Sin tipo",
                                  developmentStatus)).Distinct().ToListAsync(cancellationToken);
        var images = await db.MediaFiles.AsNoTracking().Where(x => x.ProjectId == projectId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted)
            .OrderBy(x => x.SortOrder).Select(x => new AdminImage(x.Id, x.OriginalFileName, x.AltText, x.IsCover, x.SortOrder, x.SizeBytes)).ToListAsync(cancellationToken);
        var publicVersion = publication?.PublishedProjectVersionId;
        var publicNumber = publicVersion.HasValue ? versions.FirstOrDefault(x => x.Id == publicVersion)?.Number : null;
        var likes = await db.ProjectLikes.CountAsync(x => x.ProjectId == projectId, cancellationToken);
        return new PublicationAdminDetails(projectId, versions.FirstOrDefault(x => x.Id == project.LatestApprovedVersionId)?.Name ?? versions.FirstOrDefault()?.Name ?? "Proyecto", responsible?.FullName ?? "Alumno", project.CurrentStatus, publicVersion, publicNumber, publication?.IsPublished ?? false, publication?.IsFeatured ?? false, likes, versions, images);
    }

    public async Task<OperationResult> PublishAsync(int projectId, int versionId, string administratorId, string administratorName, bool featured, CancellationToken cancellationToken = default)
    {
        var valid = await (from v in db.ProjectVersions
                           join r in db.ProjectReviews on v.Id equals r.ProjectVersionId
                           where v.Id == versionId && v.ProjectId == projectId && r.Decision == ProjectReviewDecision.Approved
                           select v.Id).AnyAsync(cancellationToken);
        if (!valid) return OperationResult.Failure("Sólo puedes publicar una versión aprobada de este proyecto.");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var publication = await db.ProjectPublications.SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);
        var wasPublished = publication?.IsPublished == true;
        var participants = await (from p in db.ProjectParticipants.AsNoTracking()
                                  join c in db.Careers.AsNoTracking() on p.CareerId equals c.Id into careerJoin
                                  from c in careerJoin.DefaultIfEmpty()
                                  where p.ProjectId == projectId orderby p.IsResponsible descending, p.FullName
                                  select new PublicParticipant(p.FullName, c == null ? null : c.Name)).ToListAsync(cancellationToken);
        var participantsSnapshot = JsonSerializer.Serialize(participants);
        if (publication is null)
        {
            publication = new ProjectPublication { ProjectId = projectId, PublishedProjectVersionId = versionId, ParticipantsSnapshot = participantsSnapshot, IsPublished = true, IsFeatured = featured, PublishedAtUtc = DateTimeOffset.UtcNow, PublishedByUserId = administratorId };
            db.ProjectPublications.Add(publication);
        }
        else
        {
            publication.PublishedProjectVersionId = versionId;
            publication.ParticipantsSnapshot = participantsSnapshot;
            publication.IsPublished = true;
            publication.IsFeatured = featured;
            publication.PublishedAtUtc = DateTimeOffset.UtcNow;
            publication.HiddenAtUtc = null;
            publication.PublishedByUserId = administratorId;
            publication.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        Audit(administratorId, administratorName, wasPublished ? "Publicacion.VersionCambiada" : "Publicacion.Publicada", projectId, $"Versión {versionId}; destacado={featured}.");
        await db.SaveChangesAsync(cancellationToken);
        var ownerId = await db.Projects.Where(x => x.Id == projectId).Select(x => x.ResponsibleUserId).SingleAsync(cancellationToken);
        var n = await notifications.CreateForUserAsync(ownerId, new NotificationDraft(NotificationType.ProjectPublished, "Proyecto publicado", "Tu proyecto ya está disponible en el portal público.", "Project", projectId.ToString(), $"/proyectos/{projectId}", $"project.published.{projectId}.{versionId}"), cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await notifications.PublishAsync(n, cancellationToken);
        return OperationResult.Success(wasPublished ? "La versión pública fue actualizada." : "El proyecto ya está publicado.");
    }

    public Task<OperationResult> SetVisibilityAsync(int projectId, string administratorId, string administratorName, bool visible, CancellationToken cancellationToken = default) =>
        SetPublicationFlagAsync(projectId, administratorId, administratorName, visible, null, cancellationToken);

    public Task<OperationResult> SetFeaturedAsync(int projectId, string administratorId, string administratorName, bool featured, CancellationToken cancellationToken = default) =>
        SetPublicationFlagAsync(projectId, administratorId, administratorName, null, featured, cancellationToken);

    private async Task<OperationResult> SetPublicationFlagAsync(int projectId, string actorId, string actorName, bool? visible, bool? featured, CancellationToken token)
    {
        var publication = await db.ProjectPublications.SingleOrDefaultAsync(x => x.ProjectId == projectId, token);
        if (publication is null) return OperationResult.Failure("El proyecto aún no tiene una publicación.");
        await using var tx = visible == false ? await db.Database.BeginTransactionAsync(token) : null;
        if (visible.HasValue) { publication.IsPublished = visible.Value; publication.HiddenAtUtc = visible.Value ? null : DateTimeOffset.UtcNow; }
        if (featured.HasValue) publication.IsFeatured = featured.Value;
        publication.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var action = visible.HasValue ? (visible.Value ? "Publicacion.Republicada" : "Publicacion.Ocultada") : (featured!.Value ? "Publicacion.Destacada" : "Publicacion.DestacadoRetirado");
        Audit(actorId, actorName, action, projectId, "Se actualizó el estado público.");
        await db.SaveChangesAsync(token);
        if (visible == false)
        {
            var ownerId = await db.Projects.Where(x => x.Id == projectId).Select(x => x.ResponsibleUserId).SingleAsync(token);
            var noticeIds = await notifications.CreateForUserAsync(ownerId, new NotificationDraft(NotificationType.ProjectHidden, "Proyecto ocultado", "La publicación de tu proyecto fue ocultada del portal público.", "Project", projectId.ToString(), $"/Student/Projects/Details/{projectId}", $"project.hidden.{projectId}.{publication.UpdatedAtUtc.Ticks}"), token);
            await tx!.CommitAsync(token);
            await notifications.PublishAsync(noticeIds, token);
        }
        return OperationResult.Success("Se actualizó el estado público del proyecto.");
    }

    public async Task<OperationResult> UploadImageAsync(int projectId, string actorId, string actorName, string originalName, string contentType, long length, Stream content, string? altText, CancellationToken token = default)
    {
        if (!await db.Projects.AnyAsync(x => x.Id == projectId && x.IsActive && x.LatestApprovedVersionId != null, token)) return OperationResult.Failure("Sólo proyectos aprobados pueden administrar imágenes.");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var allowed = extension switch { ".jpg" or ".jpeg" when contentType == "image/jpeg" => (".jpg", "image/jpeg"), ".png" when contentType == "image/png" => (".png", "image/png"), ".webp" when contentType == "image/webp" => (".webp", "image/webp"), _ => (string.Empty, string.Empty) };
        if (allowed.Item1.Length == 0) return OperationResult.Failure("Usa una imagen JPG, PNG o WebP válida.");
        if (length <= 0 || length > MaxImageBytes) return OperationResult.Failure("Cada imagen debe pesar como máximo 5 MB.");
        await using var buffer = new MemoryStream((int)length);
        await content.CopyToAsync(buffer, token);
        if (buffer.Length != length || !HasImageSignature(buffer.ToArray(), allowed.Item2)) return OperationResult.Failure("El contenido no coincide con el formato de imagen seleccionado.");
        buffer.Position = 0;
        StoredFileDescriptor descriptor;
        try { descriptor = await storage.SaveAsync(buffer, $"projects/{projectId}/images", allowed.Item1, token); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogError(ex, "Error al guardar imagen del proyecto {ProjectId}.", projectId); return OperationResult.Failure("No se pudo guardar la imagen en el servidor."); }
        var display = Path.GetFileName(originalName.Replace('\\', '/'))[..Math.Min(Path.GetFileName(originalName.Replace('\\', '/')).Length, 255)];
        var image = new MediaFile { ProjectId = projectId, ProjectVersionId = null, OriginalFileName = display, StoredFileName = descriptor.StoredFileName, RelativePath = descriptor.RelativePath, MimeType = allowed.Item2, SizeBytes = descriptor.SizeBytes, FileType = StoredFileType.ProjectImage, UploadedByUserId = actorId, CreatedAtUtc = DateTimeOffset.UtcNow, SortOrder = (await db.MediaFiles.Where(x => x.ProjectId == projectId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted).Select(x => (int?)x.SortOrder).MaxAsync(token) ?? -1) + 1, AltText = Normalize(altText, 300) };
        var isFirst = !await db.MediaFiles.AnyAsync(x => x.ProjectId == projectId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted, token);
        image.IsCover = isFirst;
        db.MediaFiles.Add(image);
        Audit(actorId, actorName, "Publicacion.ImagenSubida", projectId, $"{display} ({descriptor.SizeBytes} bytes).");
        try { await db.SaveChangesAsync(token); }
        catch { await storage.DeleteAsync(descriptor.RelativePath, CancellationToken.None); throw; }
        return OperationResult.Success("La imagen se agregó a la publicación.");
    }

    public async Task<OperationResult> SetCoverAsync(int projectId, long mediaId, string actorId, string actorName, CancellationToken token = default)
    {
        var images = await db.MediaFiles.Where(x => x.ProjectId == projectId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted).ToListAsync(token);
        if (images.All(x => x.Id != mediaId)) return OperationResult.Failure("La imagen no pertenece a este proyecto.");
        foreach (var image in images) image.IsCover = image.Id == mediaId;
        Audit(actorId, actorName, "Publicacion.PortadaCambiada", projectId, $"Imagen {mediaId} seleccionada como portada.");
        await db.SaveChangesAsync(token);
        return OperationResult.Success("La portada fue actualizada.");
    }

    public async Task<OperationResult> ReorderImageAsync(int projectId, long mediaId, int order, string actorId, string actorName, CancellationToken token = default)
    {
        var image = await db.MediaFiles.SingleOrDefaultAsync(x => x.Id == mediaId && x.ProjectId == projectId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted, token);
        if (image is null) return OperationResult.Failure("La imagen no pertenece a este proyecto.");
        image.SortOrder = Math.Clamp(order, 0, 1000);
        Audit(actorId, actorName, "Publicacion.ImagenReordenada", projectId, $"Imagen {mediaId}; posición {image.SortOrder}.");
        await db.SaveChangesAsync(token);
        return OperationResult.Success("Se actualizó el orden de la imagen.");
    }

    public async Task<OperationResult> DeleteImageAsync(int projectId, long mediaId, string actorId, string actorName, CancellationToken token = default)
    {
        var images = await db.MediaFiles.Where(x => x.ProjectId == projectId && x.FileType == StoredFileType.ProjectImage && !x.IsDeleted)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(token);
        var image = images.SingleOrDefault(x => x.Id == mediaId);
        if (image is null) return OperationResult.Failure("La imagen no pertenece a este proyecto.");
        var remaining = images.Where(x => x.Id != mediaId).ToList();
        var cover = remaining.FirstOrDefault(x => x.IsCover) ?? remaining.FirstOrDefault();
        image.IsDeleted = true;
        image.IsCover = false;
        foreach (var remainingImage in remaining) remainingImage.IsCover = remainingImage == cover;
        Audit(actorId, actorName, "Publicacion.ImagenEliminada", projectId, $"Se retiró {image.OriginalFileName}.");
        await db.SaveChangesAsync(token);
        try
        {
            if (!await storage.DeleteAsync(image.RelativePath, token)) logger.LogWarning("Imagen {MediaId} faltaba al borrar metadato lógico.", mediaId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogError(ex, "No se pudo borrar imagen {MediaId}.", mediaId); }
        return OperationResult.Success("La imagen se retiró de la publicación.");
    }

    public async Task<OperationResult<bool>> ToggleLikeAsync(string userId, int projectId, CancellationToken token = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.AccountStatus != AccountStatus.Approved || !await users.IsInRoleAsync(user, SystemRoles.Student))
            return OperationResult<bool>.Failure("Sólo alumnos con cuenta aprobada pueden dar like.");
        if (!await db.ProjectPublications.AnyAsync(x => x.ProjectId == projectId && x.IsPublished && x.Project!.IsActive, token)) return OperationResult<bool>.Failure("El proyecto no está publicado.");
        var like = await db.ProjectLikes.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId, token);
        if (like is null)
        {
            db.ProjectLikes.Add(new ProjectLike { ProjectId = projectId, UserId = userId });
            try { await db.SaveChangesAsync(token); }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); return OperationResult<bool>.Success(true, "Like guardado."); }
            return OperationResult<bool>.Success(true, "Like guardado.");
        }
        db.ProjectLikes.Remove(like);
        await db.SaveChangesAsync(token);
        return OperationResult<bool>.Success(false, "Like retirado.");
    }

    private IQueryable<PublicationJoin> PublicQuery() => from publication in db.ProjectPublications.AsNoTracking()
        join project in db.Projects.AsNoTracking() on publication.ProjectId equals project.Id
        join version in db.ProjectVersions.AsNoTracking() on publication.PublishedProjectVersionId equals version.Id
        join developmentVersion in db.ProjectVersions.AsNoTracking() on project.LatestApprovedVersionId equals developmentVersion.Id
        where publication.IsPublished && project.IsActive && project.LatestApprovedVersionId != null
            && db.ProjectReviews.Any(r => r.ProjectVersionId == version.Id && r.Decision == ProjectReviewDecision.Approved)
        select new PublicationJoin { Publication = publication, Project = project, Version = version, DevelopmentVersion = developmentVersion };

    private static PublicProjectCard Card(int id, int versionId, string name, string career, string subject, string type, string cycle, ProjectDevelopmentStatus status, string summary, long? cover, int likes, bool liked, bool featured, DateTimeOffset publishedAt) =>
        new(id, versionId, name, career, subject, type, cycle, status, summary, cover is null ? null : $"/media/projects/{cover}", likes, liked, featured, publishedAt);

    private static bool HasImageSignature(byte[] data, string mime) => mime switch
    {
        "image/jpeg" => data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
        "image/png" => data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    private void Audit(string userId, string name, string action, int projectId, string summary) => db.AuditLogs.Add(new AuditLog { UserId = userId, UserDisplayName = name, Action = action, EntityType = "Proyecto", EntityId = projectId.ToString(), Summary = summary });
    private static string? Normalize(string? text, int max) { var value = text?.Trim(); return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, max)]; }
    private sealed class PublicationJoin { public required Project Project { get; init; } public required ProjectPublication Publication { get; init; } public required ProjectVersion Version { get; init; } public required ProjectVersion DevelopmentVersion { get; init; } }
}
