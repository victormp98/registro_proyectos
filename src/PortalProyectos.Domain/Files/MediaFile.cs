using System.ComponentModel.DataAnnotations;
using PortalProyectos.Domain.News;

namespace PortalProyectos.Domain.Files;

public sealed class MediaFile
{
    public long Id { get; set; }
    public int? ProjectId { get; set; }
    public int? ProjectVersionId { get; set; }
    public int? NewsPostId { get; set; }
    public NewsPost? NewsPost { get; set; }
    [MaxLength(255)] public required string OriginalFileName { get; set; }
    [MaxLength(80)] public required string StoredFileName { get; set; }
    [MaxLength(500)] public required string RelativePath { get; set; }
    [MaxLength(100)] public required string MimeType { get; set; }
    [MaxLength(300)] public string? AltText { get; set; }
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
    public StoredFileType FileType { get; set; }
    [MaxLength(450)] public required string UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
}
