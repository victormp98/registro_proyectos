using PortalProyectos.Application.Common;

namespace PortalProyectos.Application.Notifications;

public interface IProjectDocumentService
{
    Task<ProjectDocumentItem?> GetForProjectOwnerAsync(string userId, int projectId, CancellationToken cancellationToken = default);
    Task<ProjectDocumentItem?> GetForLinkedParticipantAsync(string userId, int projectId, CancellationToken cancellationToken = default);
    Task<ProjectDocumentItem?> GetForAdministratorAsync(string administratorId, int projectId, CancellationToken cancellationToken = default);
    Task<OperationResult> UploadAsync(string userId, string actorName, int projectId, string originalFileName, string contentType, long length, Stream content, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(string userId, string actorName, int projectId, CancellationToken cancellationToken = default);
    Task<(Stream Content, ProjectDocumentItem Document)?> OpenForProjectOwnerAsync(string userId, int projectId, CancellationToken cancellationToken = default);
    Task<(Stream Content, ProjectDocumentItem Document)?> OpenForLinkedParticipantAsync(string userId, int projectId, CancellationToken cancellationToken = default);
    Task<(Stream Content, ProjectDocumentItem Document)?> OpenForAdministratorAsync(string administratorId, int projectId, CancellationToken cancellationToken = default);
}
