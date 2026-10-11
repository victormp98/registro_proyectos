using Microsoft.AspNetCore.Identity;
using PortalProyectos.Infrastructure.Identity;

namespace PortalProyectos.Web.Middleware;

public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    private static readonly PathString ChangePasswordPath = new("/cuenta/cambiar-contrasena");
    private static readonly PathString LogoutPath = new("/cuenta/cerrar-sesion");

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated == true && !IsAllowedPath(context.Request.Path))
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user?.MustChangePassword == true)
            {
                context.Response.Redirect(ChangePasswordPath);
                return;
            }
        }

        await next(context);
    }

    private static bool IsAllowedPath(PathString path) =>
        path.StartsWithSegments(ChangePasswordPath) ||
        path.StartsWithSegments(LogoutPath) ||
        path.StartsWithSegments("/css") ||
        path.StartsWithSegments("/js") ||
        path.StartsWithSegments("/lib") ||
        path.StartsWithSegments("/assets") ||
        path.StartsWithSegments("/favicon.ico");
}
