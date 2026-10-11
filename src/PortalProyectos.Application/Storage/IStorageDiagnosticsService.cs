namespace PortalProyectos.Application.Storage;

public interface IStorageDiagnosticsService
{
    Task<StorageDiagnosticSnapshot> ScanAsync(CancellationToken cancellationToken = default);
}
