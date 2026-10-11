using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PortalProyectos.Application.Storage;
using PortalProyectos.Infrastructure.Configuration;
using PortalProyectos.Infrastructure.Persistence;

namespace PortalProyectos.Infrastructure.Services;

internal sealed class StorageDiagnosticsService(PortalDbContext db, IHostEnvironment environment, IOptions<StorageOptions> options) : IStorageDiagnosticsService
{
    public async Task<StorageDiagnosticSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        var configured = options.Value.RootPath;
        var root = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured));
        var rows = await db.MediaFiles.AsNoTracking().Where(x => !x.IsDeleted)
            .Select(x => new { x.RelativePath, x.SizeBytes }).ToListAsync(cancellationToken);
        if (!Directory.Exists(root))
        {
            return new StorageDiagnosticSnapshot(root, rows.Count, 0, 0, rows.Sum(x => x.SizeBytes),
                rows.Select(x => new StoragePathIssue(x.RelativePath, x.SizeBytes, "No existe el directorio configurado.")).ToArray(), []);
        }
        var referenced = rows.ToDictionary(x => Normalize(x.RelativePath), x => x.SizeBytes, PathComparer());
        var physical = new List<(string Relative, long Bytes)>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Normalize(Path.GetRelativePath(root, file));
            physical.Add((relative, new FileInfo(file).Length));
        }
        var existing = physical.Select(x => x.Relative).ToHashSet(PathComparer());
        var missing = rows.Where(x => !existing.Contains(Normalize(x.RelativePath)))
            .Select(x => new StoragePathIssue(x.RelativePath, x.SizeBytes, "Existe referencia activa sin archivo físico.")).ToArray();
        var orphan = physical.Where(x => !referenced.ContainsKey(x.Relative))
            .Select(x => new StoragePathIssue(x.Relative, x.Bytes, "Archivo físico sin metadato activo; no se elimina automáticamente.")).ToArray();
        return new StorageDiagnosticSnapshot(root, rows.Count, physical.Count, physical.Sum(x => x.Bytes), rows.Sum(x => x.SizeBytes), missing, orphan);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static StringComparer PathComparer() => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
