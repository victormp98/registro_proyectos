using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortalProyectos.Application.Common;
using PortalProyectos.Application.News;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Files;
using PortalProyectos.Domain.News;
using PortalProyectos.Domain.Security;
using PortalProyectos.Infrastructure.Configuration;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class NewsPostService(
    PortalDbContext db,
    UserManager<ApplicationUser> users,
    IFileStorageService storage,
    IOptions<StorageOptions> storageOptions,
    ILogger<NewsPostService> logger) : INewsPostService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    public async Task<NewsPage> GetPublicPageAsync(string? search, int page, CancellationToken token = default)
    {
        const int pageSize = 9;
        page = Math.Max(1, page);
        var query = db.NewsPosts.AsNoTracking().Where(x => x.Status == NewsPostStatus.Published);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Title.Contains(term) || x.Summary.Contains(term));
        }
        var total = await query.CountAsync(token);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var posts = await query.OrderByDescending(x => x.IsFeatured).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Title, x.Summary, x.PublishedAtUtc, x.IsFeatured,
                CoverId = db.MediaFiles.Where(m => m.NewsPostId == x.Id && m.FileType == StoredFileType.NewsImage && !m.IsDeleted && m.IsCover).Select(m => (long?)m.Id).FirstOrDefault() })
            .ToListAsync(token);
        return new NewsPage(posts.Select(x => new NewsCard(x.Id, x.Title, x.Summary, x.PublishedAtUtc, x.IsFeatured, x.CoverId is null ? null : $"/media/news/{x.CoverId}")).ToArray(), page, pageSize, total, totalPages);
    }

    public async Task<NewsPostDetails?> GetPublicDetailsAsync(int id, CancellationToken token = default)
    {
        var post = await db.NewsPosts.AsNoTracking().Where(x => x.Id == id && x.Status == NewsPostStatus.Published)
            .Select(x => new { Post = x, Author = db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault() }).SingleOrDefaultAsync(token);
        return post is null ? null : await DetailsAsync(post.Post, post.Author ?? "Administración", token);
    }

    public async Task<IReadOnlyList<NewsAdminItem>> GetAdminItemsAsync(NewsFilter filter, CancellationToken token = default)
    {
        var query = db.NewsPosts.AsNoTracking().AsQueryable();
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.FeaturedOnly) query = query.Where(x => x.IsFeatured);
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Title.Contains(filter.Search.Trim()));
        var result = await query.Select(x => new { x.Id, x.Title, x.Status, x.IsFeatured, x.CreatedByUserId, x.CreatedAtUtc, x.PublishedAtUtc }).ToListAsync(token);
        var authorIds = result.Select(x => x.CreatedByUserId).Distinct().ToArray();
        var authors = await db.Users.AsNoTracking().Where(x => authorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, token);
        return result.OrderByDescending(x => x.Id).Select(x => new NewsAdminItem(x.Id, x.Title, x.Status, x.IsFeatured, authors.GetValueOrDefault(x.CreatedByUserId, "Administración"), x.CreatedAtUtc, x.PublishedAtUtc)).ToArray();
    }

    public async Task<NewsPostDetails?> GetAdminDetailsAsync(int id, CancellationToken token = default)
    {
        var post = await db.NewsPosts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token);
        if (post is null) return null;
        var author = await db.Users.AsNoTracking().Where(x => x.Id == post.CreatedByUserId).Select(x => x.FullName).SingleOrDefaultAsync(token);
        return await DetailsAsync(post, author ?? "Administración", token);
    }

    public async Task<IReadOnlyList<NewsCard>> GetFeaturedAsync(int count, CancellationToken token = default)
    {
        count = Math.Clamp(count, 1, 8);
        var posts = await db.NewsPosts.AsNoTracking().Where(x => x.Status == NewsPostStatus.Published)
            .OrderByDescending(x => x.IsFeatured).ThenByDescending(x => x.Id).Take(count)
            .Select(x => new { x.Id, x.Title, x.Summary, x.PublishedAtUtc, x.IsFeatured,
                CoverId = db.MediaFiles.Where(m => m.NewsPostId == x.Id && m.FileType == StoredFileType.NewsImage && !m.IsDeleted && m.IsCover).Select(m => (long?)m.Id).FirstOrDefault() })
            .ToListAsync(token);
        return posts.Select(x => new NewsCard(x.Id, x.Title, x.Summary, x.PublishedAtUtc, x.IsFeatured, x.CoverId is null ? null : $"/media/news/{x.CoverId}")).ToArray();
    }

    public async Task<OperationResult<int>> SaveDraftAsync(int? id, string actorId, string actorName, NewsPostInput input, CancellationToken token = default)
    {
        if (!await IsAdministratorAsync(actorId)) return OperationResult<int>.Failure("Sólo administración puede gestionar noticias.");
        var title = Normalize(input.Title, 160); var summary = Normalize(input.Summary, 500); var content = Normalize(input.Content, 12000);
        if (title is null || title.Length < 5) return OperationResult<int>.Failure("El título debe tener entre 5 y 160 caracteres.");
        if (summary is null || summary.Length < 10) return OperationResult<int>.Failure("El resumen debe tener entre 10 y 500 caracteres.");
        if (content is null || content.Length < 30) return OperationResult<int>.Failure("El contenido debe tener al menos 30 caracteres.");
        var now = DateTimeOffset.UtcNow;
        NewsPost post;
        if (id.HasValue)
        {
            post = await db.NewsPosts.SingleOrDefaultAsync(x => x.Id == id.Value, token) ?? null!;
            if (post is null) return OperationResult<int>.Failure("No se encontró la noticia.");
            post.Title = title; post.Summary = summary; post.Content = content; post.IsFeatured = input.IsFeatured; post.UpdatedAtUtc = now;
            if (post.Status == NewsPostStatus.Hidden) post.Status = NewsPostStatus.Draft;
            Audit(actorId, actorName, "Noticia.Editada", post.Id.ToString(), "Se actualizaron título, resumen o contenido.");
        }
        else
        {
            post = new NewsPost { Title = title, Summary = summary, Content = content, IsFeatured = input.IsFeatured, CreatedByUserId = actorId, CreatedAtUtc = now, UpdatedAtUtc = now };
            db.NewsPosts.Add(post);
            await db.SaveChangesAsync(token);
            Audit(actorId, actorName, "Noticia.Creada", post.Id.ToString(), $"Borrador: {title}.");
            await db.SaveChangesAsync(token);
            return OperationResult<int>.Success(post.Id, "Borrador guardado.");
        }
        await db.SaveChangesAsync(token);
        return OperationResult<int>.Success(post.Id, "Borrador guardado.");
    }

    public async Task<OperationResult> PublishAsync(int id, string actorId, string actorName, CancellationToken token = default)
    {
        if (!await IsAdministratorAsync(actorId)) return OperationResult.Failure("Sólo administración puede publicar noticias.");
        var post = await db.NewsPosts.SingleOrDefaultAsync(x => x.Id == id, token);
        if (post is null) return OperationResult.Failure("No se encontró la noticia.");
        if (!IsValidPost(post)) return OperationResult.Failure("Completa título, resumen y contenido antes de publicar.");
        post.Status = NewsPostStatus.Published; post.PublishedAtUtc ??= DateTimeOffset.UtcNow; post.HiddenAtUtc = null; post.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Audit(actorId, actorName, "Noticia.Publicada", id.ToString(), $"Se publicó: {post.Title}.");
        await db.SaveChangesAsync(token);
        return OperationResult.Success("La noticia ya está publicada.");
    }

    public async Task<OperationResult> SetVisibilityAsync(int id, string actorId, string actorName, bool visible, CancellationToken token = default)
    {
        if (!await IsAdministratorAsync(actorId)) return OperationResult.Failure("Sólo administración puede cambiar la publicación.");
        var post = await db.NewsPosts.SingleOrDefaultAsync(x => x.Id == id, token);
        if (post is null) return OperationResult.Failure("No se encontró la noticia.");
        if (visible && !IsValidPost(post)) return OperationResult.Failure("Completa los campos obligatorios antes de publicar.");
        post.Status = visible ? NewsPostStatus.Published : NewsPostStatus.Hidden;
        post.PublishedAtUtc ??= visible ? DateTimeOffset.UtcNow : null;
        post.HiddenAtUtc = visible ? null : DateTimeOffset.UtcNow; post.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Audit(actorId, actorName, visible ? "Noticia.Republicada" : "Noticia.Ocultada", id.ToString(), post.Title);
        await db.SaveChangesAsync(token);
        return OperationResult.Success(visible ? "La noticia volvió a publicarse." : "La noticia se ocultó del portal.");
    }

    public async Task<OperationResult> SetFeaturedAsync(int id, string actorId, string actorName, bool featured, CancellationToken token = default)
    {
        if (!await IsAdministratorAsync(actorId)) return OperationResult.Failure("Sólo administración puede destacar noticias.");
        var post = await db.NewsPosts.SingleOrDefaultAsync(x => x.Id == id, token);
        if (post is null) return OperationResult.Failure("No se encontró la noticia.");
        post.IsFeatured = featured; post.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Audit(actorId, actorName, featured ? "Noticia.Destacada" : "Noticia.DestacadoRetirado", id.ToString(), post.Title);
        await db.SaveChangesAsync(token);
        return OperationResult.Success("Se actualizó el destacado.");
    }

    public async Task<OperationResult> DeleteDraftAsync(int id, string actorId, string actorName, CancellationToken token = default)
    {
        if (!await IsAdministratorAsync(actorId)) return OperationResult.Failure("Sólo administración puede eliminar borradores.");
        var post = await db.NewsPosts.SingleOrDefaultAsync(x => x.Id == id && x.Status == NewsPostStatus.Draft && x.PublishedAtUtc == null, token);
        if (post is null) return OperationResult.Failure("Sólo se pueden eliminar borradores que nunca se publicaron.");
        var media = await db.MediaFiles.Where(x => x.NewsPostId == id).ToListAsync(token);
        foreach (var item in media)
        {
            try { await storage.DeleteAsync(item.RelativePath, token); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogError(ex, "No se pudo limpiar el medio {MediaId} del borrador {NewsId}.", item.Id, id); return OperationResult.Failure("No se pudo limpiar un archivo del borrador; se conservó el registro para evitar perder referencias."); }
        }
        db.MediaFiles.RemoveRange(media);
        db.NewsPosts.Remove(post);
        Audit(actorId, actorName, "Noticia.BorradorEliminado", id.ToString(), post.Title);
        await db.SaveChangesAsync(token);
        return OperationResult.Success("El borrador y sus archivos se eliminaron.");
    }

    public async Task<OperationResult> UploadImageAsync(int id, string actorId, string actorName, string fileName, string contentType, long length, Stream content, string? altText, bool cover, CancellationToken token = default)
    {
        var post = await EditablePostAsync(id, actorId, token);
        if (post is null) return OperationResult.Failure("No se encontró una noticia editable o no tienes permiso.");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var format = ext switch { ".jpg" or ".jpeg" when contentType == "image/jpeg" => (".jpg", "image/jpeg"), ".png" when contentType == "image/png" => (".png", "image/png"), ".webp" when contentType == "image/webp" => (".webp", "image/webp"), _ => (string.Empty, string.Empty) };
        if (format.Item1.Length == 0 || length <= 0 || length > MaxImageBytes) return OperationResult.Failure("Usa JPG, PNG o WebP de máximo 5 MB.");
        await using var buffer = new MemoryStream((int)length); await content.CopyToAsync(buffer, token);
        var data = buffer.ToArray(); if (buffer.Length != length || !HasImageSignature(data, format.Item2)) return OperationResult.Failure("El contenido no coincide con el formato de imagen.");
        buffer.Position = 0;
        var saved = await SavePhysicalAsync(buffer, $"news/{id}/images", format.Item1, token);
        if (saved is null) return OperationResult.Failure("No se pudo guardar la imagen en storage.");
        var active = await db.MediaFiles.Where(x => x.NewsPostId == id && x.FileType == StoredFileType.NewsImage && !x.IsDeleted).ToListAsync(token);
        var makeCover = cover || active.Count == 0 || !active.Any(x => x.IsCover);
        if (makeCover) foreach (var image in active) image.IsCover = false;
        var original = SafeName(fileName);
        db.MediaFiles.Add(new MediaFile { NewsPostId = id, OriginalFileName = original, StoredFileName = saved.StoredFileName, RelativePath = saved.RelativePath, MimeType = format.Item2, SizeBytes = saved.SizeBytes, FileType = StoredFileType.NewsImage, UploadedByUserId = actorId, SortOrder = active.Select(x => x.SortOrder).DefaultIfEmpty(-1).Max() + 1, IsCover = makeCover, AltText = Normalize(altText, 300) });
        Audit(actorId, actorName, "Noticia.ImagenSubida", id.ToString(), original);
        try { await db.SaveChangesAsync(token); } catch { await storage.DeleteAsync(saved.RelativePath, CancellationToken.None); throw; }
        return OperationResult.Success("La imagen se agregó a la noticia.");
    }

    public async Task<OperationResult> UploadVideoAsync(int id, string actorId, string actorName, string fileName, string contentType, long length, Stream content, CancellationToken token = default)
    {
        var post = await EditablePostAsync(id, actorId, token);
        if (post is null) return OperationResult.Failure("No se encontró una noticia editable o no tienes permiso.");
        var max = Math.Clamp(storageOptions.Value.MaxNewsVideoBytes, 1, 500 * 1024 * 1024);
        if (!string.Equals(Path.GetExtension(fileName), ".mp4", StringComparison.OrdinalIgnoreCase) || !string.Equals(contentType, "video/mp4", StringComparison.OrdinalIgnoreCase)) return OperationResult.Failure("Sólo se acepta video MP4.");
        if (length <= 12 || length > max) return OperationResult.Failure($"El video debe pesar como máximo {max / (1024 * 1024)} MB.");
        await using var buffer = new MemoryStream((int)length); await content.CopyToAsync(buffer, token);
        var data = buffer.ToArray();
        if (data.LongLength != length || data.Length < 12 || !data.AsSpan(4, 4).SequenceEqual("ftyp"u8)) return OperationResult.Failure("El archivo no parece contener un contenedor MP4 válido.");
        buffer.Position = 0;
        var saved = await SavePhysicalAsync(buffer, $"news/{id}/videos", ".mp4", token);
        if (saved is null) return OperationResult.Failure("No se pudo guardar el video en storage.");
        var old = await db.MediaFiles.Where(x => x.NewsPostId == id && x.FileType == StoredFileType.NewsVideo && !x.IsDeleted).ToListAsync(token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        foreach (var item in old) item.IsDeleted = true;
        db.MediaFiles.Add(new MediaFile { NewsPostId = id, OriginalFileName = SafeName(fileName), StoredFileName = saved.StoredFileName, RelativePath = saved.RelativePath, MimeType = "video/mp4", SizeBytes = saved.SizeBytes, FileType = StoredFileType.NewsVideo, UploadedByUserId = actorId, SortOrder = 0 });
        Audit(actorId, actorName, old.Count == 0 ? "Noticia.VideoSubido" : "Noticia.VideoReemplazado", id.ToString(), SafeName(fileName));
        try { await db.SaveChangesAsync(token); await tx.CommitAsync(token); }
        catch { await tx.RollbackAsync(CancellationToken.None); await storage.DeleteAsync(saved.RelativePath, CancellationToken.None); throw; }
        foreach (var item in old) await DeleteOldPhysicalAsync(item);
        return OperationResult.Success("El video local se guardó correctamente.");
    }

    public async Task<OperationResult> DeleteMediaAsync(int id, long mediaId, string actorId, string actorName, CancellationToken token = default)
    {
        if (await EditablePostAsync(id, actorId, token) is null) return OperationResult.Failure("No tienes permiso para cambiar los medios de esta noticia.");
        var media = await db.MediaFiles.SingleOrDefaultAsync(x => x.Id == mediaId && x.NewsPostId == id && !x.IsDeleted && (x.FileType == StoredFileType.NewsImage || x.FileType == StoredFileType.NewsVideo), token);
        if (media is null) return OperationResult.Failure("No se encontró el medio solicitado.");
        media.IsDeleted = true;
        if (media.IsCover)
        {
            media.IsCover = false;
            var next = await db.MediaFiles.Where(x => x.NewsPostId == id && x.FileType == StoredFileType.NewsImage && !x.IsDeleted && x.Id != mediaId).OrderBy(x => x.SortOrder).FirstOrDefaultAsync(token);
            if (next is not null) next.IsCover = true;
        }
        Audit(actorId, actorName, media.FileType == StoredFileType.NewsVideo ? "Noticia.VideoEliminado" : "Noticia.ImagenEliminada", id.ToString(), media.OriginalFileName);
        await db.SaveChangesAsync(token);
        await DeleteOldPhysicalAsync(media);
        return OperationResult.Success("El medio se retiró de la noticia.");
    }

    public async Task<OperationResult> SetCoverAsync(int id, long mediaId, string actorId, string actorName, CancellationToken token = default)
    {
        if (await EditablePostAsync(id, actorId, token) is null) return OperationResult.Failure("No tienes permiso para cambiar esta noticia.");
        var images = await db.MediaFiles.Where(x => x.NewsPostId == id && x.FileType == StoredFileType.NewsImage && !x.IsDeleted).ToListAsync(token);
        if (images.All(x => x.Id != mediaId)) return OperationResult.Failure("La imagen no pertenece a esta noticia.");
        foreach (var image in images) image.IsCover = image.Id == mediaId;
        Audit(actorId, actorName, "Noticia.PortadaCambiada", id.ToString(), mediaId.ToString()); await db.SaveChangesAsync(token);
        return OperationResult.Success("La portada se actualizó.");
    }

    public async Task<OperationResult> ReorderMediaAsync(int id, long mediaId, int order, string actorId, string actorName, CancellationToken token = default)
    {
        if (await EditablePostAsync(id, actorId, token) is null) return OperationResult.Failure("No tienes permiso para cambiar esta noticia.");
        var media = await db.MediaFiles.SingleOrDefaultAsync(x => x.Id == mediaId && x.NewsPostId == id && x.FileType == StoredFileType.NewsImage && !x.IsDeleted, token);
        if (media is null) return OperationResult.Failure("La imagen no pertenece a esta noticia.");
        media.SortOrder = Math.Clamp(order, 0, 1000); Audit(actorId, actorName, "Noticia.ImagenReordenada", id.ToString(), media.SortOrder.ToString()); await db.SaveChangesAsync(token);
        return OperationResult.Success("Se actualizó el orden de la galería.");
    }

    public async Task<OperationResult> SetExternalVideoAsync(int id, string? url, string actorId, string actorName, CancellationToken token = default)
    {
        var post = await EditablePostAsync(id, actorId, token);
        if (post is null) return OperationResult.Failure("No se encontró una noticia editable.");
        string? videoId = null;
        if (!string.IsNullOrWhiteSpace(url) && !TryGetYouTubeId(url.Trim(), out videoId)) return OperationResult.Failure("La URL debe ser un enlace válido de YouTube (youtube.com o youtu.be).");
        post.ExternalVideoProvider = videoId is null ? null : "YouTube"; post.ExternalVideoId = videoId; post.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Audit(actorId, actorName, videoId is null ? "Noticia.VideoExternoEliminado" : "Noticia.VideoExternoConfigurado", id.ToString(), videoId ?? "Se retiró el video externo."); await db.SaveChangesAsync(token);
        return OperationResult.Success(videoId is null ? "Se retiró el video externo." : "El video externo de YouTube quedó configurado.");
    }

    public async Task<OperationResult> AddExternalLinkAsync(int id, string label, string url, string actorId, string actorName, CancellationToken token = default)
    {
        if (await EditablePostAsync(id, actorId, token) is null) return OperationResult.Failure("No se encontró una noticia editable.");
        label = Normalize(label, 100) ?? string.Empty;
        if (label.Length < 2 || !IsSafeHttpUrl(url)) return OperationResult.Failure("Escribe una etiqueta y una URL HTTP/HTTPS válida.");
        var order = (await db.NewsExternalLinks.Where(x => x.NewsPostId == id).Select(x => (int?)x.SortOrder).MaxAsync(token) ?? -1) + 1;
        db.NewsExternalLinks.Add(new NewsExternalLink { NewsPostId = id, Label = label, Url = url.Trim(), SortOrder = order });
        Audit(actorId, actorName, "Noticia.EnlaceAgregado", id.ToString(), label); await db.SaveChangesAsync(token);
        return OperationResult.Success("El enlace se agregó.");
    }

    public async Task<OperationResult> DeleteExternalLinkAsync(int id, int linkId, string actorId, string actorName, CancellationToken token = default)
    {
        if (await EditablePostAsync(id, actorId, token) is null) return OperationResult.Failure("No se encontró una noticia editable.");
        var link = await db.NewsExternalLinks.SingleOrDefaultAsync(x => x.Id == linkId && x.NewsPostId == id, token);
        if (link is null) return OperationResult.Failure("No se encontró el enlace.");
        db.NewsExternalLinks.Remove(link); Audit(actorId, actorName, "Noticia.EnlaceEliminado", id.ToString(), link.Label); await db.SaveChangesAsync(token);
        return OperationResult.Success("El enlace se eliminó.");
    }

    public Task<(Stream Content, string MimeType, string FileName)?> OpenPublicMediaAsync(long mediaId, CancellationToken token = default) => OpenMediaAsync(mediaId, null, null, token);

    public async Task<(Stream Content, string MimeType, string FileName)?> OpenAdminMediaAsync(int id, long mediaId, string administratorId, CancellationToken token = default)
    {
        if (!await IsAdministratorAsync(administratorId)) return null;
        return await OpenMediaAsync(mediaId, id, administratorId, token);
    }

    private async Task<(Stream Content, string MimeType, string FileName)?> OpenMediaAsync(long mediaId, int? adminNewsId, string? administratorId, CancellationToken token)
    {
        var query = db.MediaFiles.AsNoTracking().Where(x => x.Id == mediaId && x.NewsPostId != null && !x.IsDeleted && (x.FileType == StoredFileType.NewsImage || x.FileType == StoredFileType.NewsVideo));
        if (adminNewsId.HasValue) query = query.Where(x => x.NewsPostId == adminNewsId);
        else query = query.Where(x => x.NewsPost!.Status == NewsPostStatus.Published);
        var media = await query.Select(x => new { x.RelativePath, x.MimeType, x.OriginalFileName }).SingleOrDefaultAsync(token);
        if (media is null) return null;
        var stream = await storage.OpenReadAsync(media.RelativePath, token);
        return stream is null ? null : (stream, media.MimeType, media.OriginalFileName);
    }

    private async Task<NewsPostDetails> DetailsAsync(NewsPost post, string author, CancellationToken token)
    {
        var media = await db.MediaFiles.AsNoTracking().Where(x => x.NewsPostId == post.Id && !x.IsDeleted && (x.FileType == StoredFileType.NewsImage || x.FileType == StoredFileType.NewsVideo))
            .OrderBy(x => x.FileType == StoredFileType.NewsVideo).ThenByDescending(x => x.IsCover).ThenBy(x => x.SortOrder)
            .Select(x => new NewsMediaItem(x.Id, x.OriginalFileName, x.AltText, x.SortOrder, x.IsCover, x.SizeBytes, x.FileType == StoredFileType.NewsVideo)).ToListAsync(token);
        var links = await db.NewsExternalLinks.AsNoTracking().Where(x => x.NewsPostId == post.Id).OrderBy(x => x.SortOrder)
            .Select(x => new NewsExternalLinkItem(x.Id, x.Label, x.Url, x.SortOrder)).ToListAsync(token);
        var embedUrl = post.ExternalVideoProvider == "YouTube" && post.ExternalVideoId is not null ? $"https://www.youtube-nocookie.com/embed/{post.ExternalVideoId}" : null;
        return new NewsPostDetails(post.Id, post.Title, post.Summary, post.Content, post.Status, post.IsFeatured, author, post.CreatedAtUtc, post.PublishedAtUtc, embedUrl, post.ExternalVideoProvider, post.ExternalVideoId, media, links);
    }

    private async Task<NewsPost?> EditablePostAsync(int id, string actorId, CancellationToken token) =>
        await IsAdministratorAsync(actorId) ? await db.NewsPosts.SingleOrDefaultAsync(x => x.Id == id, token) : null;

    private async Task<bool> IsAdministratorAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        return user is not null && (await users.IsInRoleAsync(user, SystemRoles.Administrator) || await users.IsInRoleAsync(user, SystemRoles.SuperAdministrator));
    }

    private async Task<StoredFileDescriptor?> SavePhysicalAsync(Stream stream, string directory, string extension, CancellationToken token)
    {
        try { return await storage.SaveAsync(stream, directory, extension, token); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogError(ex, "Falló el storage editorial en {Directory}.", directory); return null; }
    }

    private async Task DeleteOldPhysicalAsync(MediaFile media)
    {
        try { if (!await storage.DeleteAsync(media.RelativePath, CancellationToken.None)) logger.LogWarning("No existía archivo {MediaId} al limpiar medio editorial.", media.Id); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogError(ex, "No se pudo retirar el archivo editorial {MediaId}.", media.Id); }
    }

    private void Audit(string actorId, string actorName, string action, string entityId, string summary) => db.AuditLogs.Add(new AuditLog { UserId = actorId, UserDisplayName = actorName, Action = action, EntityType = "Noticia", EntityId = entityId, Summary = summary });
    private static bool IsValidPost(NewsPost post) => post.Title.Trim().Length >= 5 && post.Title.Length <= 160 && post.Summary.Trim().Length >= 10 && post.Summary.Length <= 500 && post.Content.Trim().Length >= 30 && post.Content.Length <= 12000;
    private static string? Normalize(string? value, int max) { var normalized = value?.Trim(); return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, max)]; }
    private static string SafeName(string name) { var fileName = Path.GetFileName(name.Replace('\\', '/')); var safe = new string(fileName.Where(c => !char.IsControl(c) && c is not '/' and not '\\').Take(255).ToArray()).Trim(); return string.IsNullOrWhiteSpace(safe) ? "medio" : safe; }
    private static bool IsSafeHttpUrl(string value) => value.Length <= 1000 && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.UserInfo.Length == 0 && uri.Scheme is "http" or "https";

    private static bool TryGetYouTubeId(string value, out string? videoId)
    {
        videoId = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0) return false;
        var host = uri.Host.ToLowerInvariant();
        if (host is "youtu.be" or "www.youtu.be") videoId = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (uri.AbsolutePath == "/watch") videoId = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query).TryGetValue("v", out var queryId) ? queryId.FirstOrDefault() : null;
            else if (segments.Length >= 2 && segments[0] is "embed" or "shorts" or "live") videoId = segments[1];
        }
        if (videoId is null || !Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)) { videoId = null; return false; }
        return true;
    }

    private static bool HasImageSignature(byte[] data, string mime) => mime switch
    {
        "image/jpeg" => data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
        "image/png" => data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
