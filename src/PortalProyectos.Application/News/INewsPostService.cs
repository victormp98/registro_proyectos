using PortalProyectos.Application.Common;
using PortalProyectos.Domain.News;

namespace PortalProyectos.Application.News;

public interface INewsPostService
{
    Task<NewsPage> GetPublicPageAsync(string? search, int page, CancellationToken cancellationToken = default);
    Task<NewsPostDetails?> GetPublicDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsAdminItem>> GetAdminItemsAsync(NewsFilter filter, CancellationToken cancellationToken = default);
    Task<NewsPostDetails?> GetAdminDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsCard>> GetFeaturedAsync(int count, CancellationToken cancellationToken = default);
    Task<OperationResult<int>> SaveDraftAsync(int? id, string administratorId, string administratorName, NewsPostInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> PublishAsync(int id, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> SetVisibilityAsync(int id, string administratorId, string administratorName, bool visible, CancellationToken cancellationToken = default);
    Task<OperationResult> SetFeaturedAsync(int id, string administratorId, string administratorName, bool featured, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteDraftAsync(int id, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> UploadImageAsync(int id, string administratorId, string administratorName, string fileName, string contentType, long length, Stream content, string? altText, bool cover, CancellationToken cancellationToken = default);
    Task<OperationResult> UploadVideoAsync(int id, string administratorId, string administratorName, string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteMediaAsync(int id, long mediaId, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> SetCoverAsync(int id, long mediaId, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> ReorderMediaAsync(int id, long mediaId, int order, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> SetExternalVideoAsync(int id, string? url, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> AddExternalLinkAsync(int id, string label, string url, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteExternalLinkAsync(int id, int linkId, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<(Stream Content, string MimeType, string FileName)?> OpenPublicMediaAsync(long mediaId, CancellationToken cancellationToken = default);
    Task<(Stream Content, string MimeType, string FileName)?> OpenAdminMediaAsync(int id, long mediaId, string administratorId, CancellationToken cancellationToken = default);
}
