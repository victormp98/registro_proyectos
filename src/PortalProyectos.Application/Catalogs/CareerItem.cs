namespace PortalProyectos.Application.Catalogs;

public sealed record CareerItem(int Id, string Name, string? Code, bool IsActive, int UsageCount = 0);
