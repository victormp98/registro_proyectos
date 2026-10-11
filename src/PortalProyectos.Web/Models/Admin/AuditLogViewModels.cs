using PortalProyectos.Domain.Auditing;

namespace PortalProyectos.Web.Models.Admin;

public sealed record AuditLogRow(long Id, DateTimeOffset CreatedAtUtc, string UserDisplayName, string Action, string EntityType, string EntityId, string? Summary);
public sealed record AuditLogPage(IReadOnlyList<AuditLogRow> Items, string? User, string? Action, string? Entity, DateOnly? From, DateOnly? To, int Page, int TotalPages, int TotalCount);
