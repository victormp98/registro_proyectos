namespace PortalProyectos.Application.Storage;

public sealed record StoragePathIssue(string RelativePath, long MetadataBytes, string Description);
public sealed record StorageDiagnosticSnapshot(string RootPath, int ActiveReferences, int PhysicalFiles, long PhysicalBytes, long ReferencedBytes, IReadOnlyList<StoragePathIssue> MissingFiles, IReadOnlyList<StoragePathIssue> OrphanFiles);
