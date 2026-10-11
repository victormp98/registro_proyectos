using PortalProyectos.Domain.Files;

namespace PortalProyectos.Application.Notifications;

public interface IFileStorageService
{
    Task<StoredFileDescriptor> SaveAsync(Stream content, string relativeDirectory, string extension, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
