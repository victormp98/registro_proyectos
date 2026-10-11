using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Web.Models.Notifications;

namespace PortalProyectos.Web.Controllers;

[Authorize]
[Route("Notifications")]
public sealed class NotificationsController(INotificationService notifications) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(bool unreadOnly = false, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await notifications.GetPageAsync(GetUserId(), unreadOnly, page, cancellationToken: cancellationToken);
        return View(new NotificationPageViewModel(result, unreadOnly));
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken)
    {
        var items = await notifications.GetRecentAsync(GetUserId(), cancellationToken: cancellationToken);
        return Ok(new NotificationRecentResponse(items.Select(x => new NotificationDropdownItem(
            x.Id, x.Type.ToString(), x.Title, x.Message, x.EntityType, x.EntityId, x.Url, x.IsRead, x.CreatedAtUtc)).ToArray(),
            await notifications.GetUnreadCountAsync(GetUserId(), cancellationToken)));
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        var result = await notifications.MarkAsReadAsync(GetUserId(), id, cancellationToken);
        if (!result) return NotFound();
        return Request.Headers.XRequestedWith == "XMLHttpRequest"
            ? Ok(new { ok = true, unreadCount = await notifications.GetUnreadCountAsync(GetUserId(), cancellationToken) })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var count = await notifications.MarkAllAsReadAsync(GetUserId(), cancellationToken);
        return Request.Headers.XRequestedWith == "XMLHttpRequest" ? Ok(new { ok = true, count }) : RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:long}/open")]
    public async Task<IActionResult> Open(long id, CancellationToken cancellationToken)
    {
        var item = await notifications.GetByIdAsync(GetUserId(), id, cancellationToken);
        if (item is null) return NotFound();
        await notifications.MarkAsReadAsync(GetUserId(), id, cancellationToken);
        return Url.IsLocalUrl(item.Url) ? LocalRedirect(item.Url!) : RedirectToAction(nameof(Index));
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No se encontró el usuario autenticado.");
}
