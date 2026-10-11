using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.News;

public sealed class NewsExternalLink
{
    public int Id { get; set; }
    public int NewsPostId { get; set; }
    public NewsPost? NewsPost { get; set; }
    [MaxLength(100)] public required string Label { get; set; }
    [MaxLength(1000)] public required string Url { get; set; }
    public int SortOrder { get; set; }
}
