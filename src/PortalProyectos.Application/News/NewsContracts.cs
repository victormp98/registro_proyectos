using PortalProyectos.Domain.News;

namespace PortalProyectos.Application.News;

public sealed record NewsPostInput(string? Title, string? Summary, string? Content, bool IsFeatured);
public sealed record NewsFilter(string? Search, NewsPostStatus? Status, bool FeaturedOnly = false);
public sealed record NewsCard(int Id, string Title, string Summary, DateTimeOffset? PublishedAtUtc, bool IsFeatured, string? CoverUrl);
public sealed record NewsMediaItem(long Id, string FileName, string? AltText, int SortOrder, bool IsCover, long SizeBytes, bool IsVideo);
public sealed record NewsExternalLinkItem(int Id, string Label, string Url, int SortOrder);
public sealed record NewsPostDetails(int Id, string Title, string Summary, string Content, NewsPostStatus Status, bool IsFeatured, string Author, DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc, string? ExternalVideoUrl, string? ExternalVideoProvider, string? ExternalVideoId, IReadOnlyList<NewsMediaItem> Media, IReadOnlyList<NewsExternalLinkItem> Links);
public sealed record NewsPage(IReadOnlyList<NewsCard> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record NewsAdminItem(int Id, string Title, NewsPostStatus Status, bool IsFeatured, string Author, DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc);

