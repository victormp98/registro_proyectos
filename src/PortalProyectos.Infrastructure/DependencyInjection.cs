using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalProyectos.Application.Catalogs;
using PortalProyectos.Application.Users;
using PortalProyectos.Infrastructure.Configuration;
using PortalProyectos.Infrastructure.Identity;
using PortalProyectos.Infrastructure.Persistence;
using PortalProyectos.Application.Projects;
using PortalProyectos.Application.Notifications;
using PortalProyectos.Infrastructure.Services;
using PortalProyectos.Application.News;
using PortalProyectos.Application.Storage;

namespace PortalProyectos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PortalDatabase")
            ?? throw new InvalidOperationException("No se configuró la conexión 'PortalDatabase'.");

        services.AddDbContext<PortalDbContext>(options => options.UseSqlite(connectionString));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<PortalDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "PortalProyectos.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.LoginPath = "/cuenta/iniciar-sesion";
            options.AccessDeniedPath = "/cuenta/acceso-denegado";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        // Hace efectivo un bloqueo o desactivación en la siguiente solicitud autenticada.
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        services.Configure<InitialAdminOptions>(configuration.GetSection(InitialAdminOptions.SectionName));
        services.Configure<ProjectOptions>(configuration.GetSection(ProjectOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSignalR();
        services.AddScoped<IAccountAdministrationService, AccountAdministrationService>();
        services.AddScoped<IStudentAccountService, StudentAccountService>();
        services.AddScoped<ICareerService, CareerService>();
        services.AddScoped<IRegistrationCatalogService, RegistrationCatalogService>();
        services.AddScoped<IProjectCatalogService, ProjectCatalogService>();
        services.AddScoped<IStudentProjectService, StudentProjectService>();
        services.AddScoped<IProjectReviewService, ProjectReviewService>();
        services.AddScoped<IProjectPublicationService, ProjectPublicationService>();
        services.AddScoped<INewsPostService, NewsPostService>();
        services.AddScoped<IStorageDiagnosticsService, StorageDiagnosticsService>();
        services.AddScoped<ProjectDetailsReader>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProjectDocumentService, ProjectDocumentService>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<INotificationPublisher, NullNotificationPublisher>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
