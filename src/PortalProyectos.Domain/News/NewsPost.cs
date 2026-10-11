using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.News;

public sealed class NewsPost
{
    public int Id { get; set; }
    [MaxLength(160)] public required string Title { get; set; }
    [MaxLength(500)] public required string Summary { get; set; }
    [MaxLength(12000)] public required string Content { get; set; }
    public NewsPostStatus Status { get; set; } = NewsPostStatus.Draft;
    public bool IsFeatured { get; set; }
    [MaxLength(450)] public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset? HiddenAtUtc { get; set; }
    [MaxLength(24)] public string? ExternalVideoProvider { get; set; }
    [MaxLength(32)] public string? ExternalVideoId { get; set; }
    public ICollection<NewsExternalLink> Links { get; set; } = [];
}
