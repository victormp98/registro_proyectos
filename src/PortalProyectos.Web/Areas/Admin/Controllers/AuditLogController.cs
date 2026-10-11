using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalProyectos.Infrastructure.Persistence;
using PortalProyectos.Web.Models.Admin;

namespace PortalProyectos.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Administracion")]
[Route("Admin/AuditLog")]
public sealed class AuditLogController(PortalDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? user,
        [FromQuery(Name = "action")] string? actionFilter,
        string? entity,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 50;
        var startDate = from?.ToString("yyyy-MM-dd");
        var endDate = to?.ToString("yyyy-MM-dd");
        var query = db.AuditLogs.FromSqlInterpolated($"SELECT * FROM \"AuditLogs\" WHERE ({startDate} IS NULL OR substr(\"CreatedAtUtc\", 1, 10) >= {startDate}) AND ({endDate} IS NULL OR substr(\"CreatedAtUtc\", 1, 10) <= {endDate})").AsNoTracking();
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(x => x.UserDisplayName.Contains(user.Trim()));
        if (!string.IsNullOrWhiteSpace(actionFilter)) query = query.Where(x => x.Action.Contains(actionFilter.Trim()));
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(x => x.EntityType.Contains(entity.Trim()) || x.EntityId.Contains(entity.Trim()));
        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditLogRow(x.Id, x.CreatedAtUtc, x.UserDisplayName, x.Action, x.EntityType, x.EntityId, x.Summary))
            .ToListAsync(cancellationToken);
        return View(new AuditLogPage(items, user, actionFilter, entity, from, to, page, totalPages, total));
    }
}
