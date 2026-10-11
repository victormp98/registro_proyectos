using System.ComponentModel.DataAnnotations;
using PortalProyectos.Application.News;
using PortalProyectos.Domain.News;

namespace PortalProyectos.Web.Models.News;

public sealed class NewsPostFormViewModel
{
    public int? Id { get; set; }
    [Required, StringLength(160, MinimumLength = 5)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 10)] public string Summary { get; set; } = string.Empty;
    [Required, StringLength(12000, MinimumLength = 30)] public string Content { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public NewsPostStatus? Status { get; set; }
    public IReadOnlyList<NewsMediaItem> Media { get; set; } = [];
    public IReadOnlyList<NewsExternalLinkItem> Links { get; set; } = [];
    public string? ExternalVideoUrl { get; set; }
    public string? Author { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public static NewsPostFormViewModel From(NewsPostDetails details) => new()
    {
        Id = details.Id, Title = details.Title, Summary = details.Summary, Content = details.Content,
        IsFeatured = details.IsFeatured, Status = details.Status, Media = details.Media, Links = details.Links,
        ExternalVideoUrl = details.ExternalVideoProvider == "YouTube" && details.ExternalVideoId is not null
            ? $"https://youtu.be/{details.ExternalVideoId}" : null,
        Author = details.Author, PublishedAtUtc = details.PublishedAtUtc
    };
}
