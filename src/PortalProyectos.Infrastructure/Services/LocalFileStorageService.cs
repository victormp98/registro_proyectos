using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Infrastructure.Configuration;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;

    public LocalFileStorageService(IHostEnvironment environment, IOptions<StorageOptions> options)
    {
        var configuredPath = options.Value.RootPath;
        _root = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath));
        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        var contentPrefix = contentRoot.EndsWith(Path.DirectorySeparatorChar) ? contentRoot : contentRoot + Path.DirectorySeparatorChar;
        if (_root.StartsWith(contentPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || string.Equals(_root, contentRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("El almacenamiento debe quedar fuera de la carpeta de la aplicación.");
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFileDescriptor> SaveAsync(Stream content, string relativeDirectory, string extension, CancellationToken cancellationToken = default)
    {
        var segments = relativeDirectory.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(x => x is "." or ".." || x.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')))
            throw new InvalidOperationException("La carpeta de almacenamiento no es válida.");
        var safeExtension = extension.ToLowerInvariant();
        if (safeExtension is not (".pdf" or ".jpg" or ".jpeg" or ".png" or ".webp" or ".mp4"))
            throw new InvalidOperationException("La extensión no está permitida para almacenamiento.");

        var relativeDirectoryPath = Path.Combine(segments);
        var destinationDirectory = ResolvePath(relativeDirectoryPath);
        Directory.CreateDirectory(destinationDirectory);
        var storedName = $"{Guid.NewGuid():N}{safeExtension}";
        var destination = ResolvePath(Path.Combine(relativeDirectoryPath, storedName));
        try
        {
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await content.CopyToAsync(output, cancellationToken);
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
        var size = new FileInfo(destination).Length;
        return new StoredFileDescriptor(storedName, Path.Combine(relativeDirectoryPath, storedName).Replace('\\', '/'), size);
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(relativePath);
        return Task.FromResult<Stream?>(File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null);
    }

    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(relativePath);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    private string ResolvePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidOperationException("No se aceptan rutas absolutas de archivo.");
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        var prefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("La ruta excede el almacenamiento configurado.");
        return fullPath;
    }
}
