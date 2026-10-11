using PortalProyectos.Application.Common;
using PortalProyectos.Domain.Projects;

namespace PortalProyectos.Application.Projects;

public interface IProjectPublicationService
{
    Task<PublicationPage> GetPublicPageAsync(PublicProjectFilter filter, int page, string? userId, CancellationToken cancellationToken = default);
    Task<PublicProjectDetails?> GetPublicDetailsAsync(int projectId, string? userId, CancellationToken cancellationToken = default);
    Task<(Stream Content, string MimeType, string? AltText)?> OpenPublicImageAsync(long mediaId, CancellationToken cancellationToken = default);
    Task<(Stream Content, string MimeType, string? AltText)?> OpenAdminImageAsync(string administratorId, long mediaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicationAdminItem>> GetAdminItemsAsync(PublicationAdminFilter filter, CancellationToken cancellationToken = default);
    Task<PublicationAdminDetails?> GetAdminDetailsAsync(int projectId, CancellationToken cancellationToken = default);
    Task<OperationResult> PublishAsync(int projectId, int versionId, string administratorId, string administratorName, bool featured, CancellationToken cancellationToken = default);
    Task<OperationResult> SetVisibilityAsync(int projectId, string administratorId, string administratorName, bool visible, CancellationToken cancellationToken = default);
    Task<OperationResult> SetFeaturedAsync(int projectId, string administratorId, string administratorName, bool featured, CancellationToken cancellationToken = default);
    Task<OperationResult> UploadImageAsync(int projectId, string administratorId, string administratorName, string originalName, string contentType, long length, Stream content, string? altText, CancellationToken cancellationToken = default);
    Task<OperationResult> SetCoverAsync(int projectId, long mediaId, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> ReorderImageAsync(int projectId, long mediaId, int order, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteImageAsync(int projectId, long mediaId, string administratorId, string administratorName, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> ToggleLikeAsync(string userId, int projectId, CancellationToken cancellationToken = default);
}

public sealed record PublicProjectFilter(string? Search, int? CareerId, int? SubjectId, int? TypeId, ProjectDevelopmentStatus? DevelopmentStatus, int? CycleId, string? Sort);
public sealed record PublicationAdminFilter(string? State, int? CareerId, int? CycleId, string? Search);
public sealed record PublicationPage(IReadOnlyList<PublicProjectCard> Items, int Page, int PageSize, int TotalCount, int TotalPages, IReadOnlyList<PublicationFilterOption> Careers, IReadOnlyList<PublicationFilterOption> Subjects, IReadOnlyList<PublicationFilterOption> Types, IReadOnlyList<PublicationFilterOption> Cycles);
public sealed record PublicationFilterOption(int Id, string Name);
public sealed record PublicProjectCard(int Id, int VersionId, string Name, string Career, string Subject, string Type, string Cycle, ProjectDevelopmentStatus DevelopmentStatus, string ProblemStatement, string? CoverUrl, int LikeCount, bool IsLiked, bool IsFeatured, DateTimeOffset PublishedAtUtc);
public sealed record PublicParticipant(string Name, string? Career);
public sealed record PublicProjectDetails(PublicProjectCard Project, string ProblemStatement, string ProposedSolution, string? Advisor, IReadOnlyList<PublicParticipant> Participants, IReadOnlyList<PublicImage> Images);
public sealed record PublicImage(string Url, string? AltText, bool IsCover, int SortOrder);
public sealed record PublicationAdminItem(int ProjectId, string Name, string Responsible, string Career, string Cycle, ProjectAdministrativeStatus ReviewStatus, bool IsPublished, bool IsFeatured, int? VersionId, int? VersionNumber, DateTimeOffset? PublishedAtUtc, int Likes);
public sealed record PublicationAdminDetails(int ProjectId, string Name, string Responsible, ProjectAdministrativeStatus Status, int? PublicVersionId, int? PublicVersionNumber, bool IsPublished, bool IsFeatured, int LikeCount, IReadOnlyList<ApprovedVersionOption> ApprovedVersions, IReadOnlyList<AdminImage> Images);
public sealed record ApprovedVersionOption(int Id, int Number, string Name, string Career, string Subject, string Cycle, string Type, ProjectDevelopmentStatus DevelopmentStatus);
public sealed record AdminImage(long Id, string Name, string? AltText, bool IsCover, int SortOrder, long SizeBytes);
