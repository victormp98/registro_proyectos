using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using PortalProyectos.Domain.Security;
using PortalProyectos.Infrastructure;
using PortalProyectos.Infrastructure.Persistence;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Web.Hubs;
using PortalProyectos.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

var databasePathSetting = builder.Configuration["Data:DatabasePath"] ?? "App_Data/portalproyectos.db";
var databasePath = Path.GetFullPath(Path.IsPathRooted(databasePathSetting)
    ? databasePathSetting
    : Path.Combine(builder.Environment.ContentRootPath, databasePathSetting));
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
builder.Configuration["ConnectionStrings:PortalDatabase"] = new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared,
    ForeignKeys = true
}.ToString();

var keysPathSetting = builder.Configuration["DataProtection:KeysPath"] ?? "App_Data/keys";
var keysDirectory = Path.GetFullPath(Path.IsPathRooted(keysPathSetting)
    ? keysPathSetting
    : Path.Combine(builder.Environment.ContentRootPath, keysPathSetting));
Directory.CreateDirectory(keysDirectory);

var configuredUrls = builder.Configuration["Server:Urls"];
if (!string.IsNullOrWhiteSpace(configuredUrls)) builder.WebHost.UseUrls(configuredUrls);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("PortalProyectos");

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administracion", policy =>
        policy.RequireRole(SystemRoles.Administrator, SystemRoles.SuperAdministrator));
    options.AddPolicy("SuperAdministracion", policy =>
        policy.RequireRole(SystemRoles.SuperAdministrator));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?statusCode={0}");

var supportedCultures = new[] { new CultureInfo("es-MX") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-MX"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (builder.Configuration.GetValue<bool>("Portal:UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.MapStaticAssets();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapHub<NotificationHub>("/hubs/notifications");

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

public partial class Program;
