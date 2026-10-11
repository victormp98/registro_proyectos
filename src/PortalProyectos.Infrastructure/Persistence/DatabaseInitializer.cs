using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortalProyectos.Domain.Catalogs;
using PortalProyectos.Domain.Security;
using PortalProyectos.Domain.Users;
using PortalProyectos.Infrastructure.Configuration;
using PortalProyectos.Infrastructure.Identity;

namespace PortalProyectos.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    PortalDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<InitialAdminOptions> initialAdminOptions,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnableSqlitePragmasAsync(cancellationToken);
        await SeedRolesAsync();
        await SeedCatalogsAsync(cancellationToken);
        await SeedSuperAdministratorAsync();
    }

    private async Task EnableSqlitePragmasAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            var mode = await command.ExecuteScalarAsync(cancellationToken);
            logger.LogInformation("SQLite journal_mode: {JournalMode}", mode);

            command.CommandText = "PRAGMA foreign_keys=ON;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in SystemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                EnsureSucceeded(result, $"crear el rol {role}");
            }
        }
    }

    private async Task SeedCatalogsAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.Careers.AnyAsync(cancellationToken))
        {
            dbContext.Careers.Add(new Career { Name = "Ingeniería en Sistemas", Code = "IS" });
        }

        if (!await dbContext.Grades.AnyAsync(cancellationToken))
        {
            dbContext.Grades.AddRange(
                Enumerable.Range(1, 10).Select(number => new Grade
                {
                    Name = $"{number}°",
                    SortOrder = number
                }));
        }

        if (!await dbContext.AcademicGroups.AnyAsync(cancellationToken))
        {
            dbContext.AcademicGroups.AddRange(
                new AcademicGroup { Name = "A", SortOrder = 1 },
                new AcademicGroup { Name = "B", SortOrder = 2 },
                new AcademicGroup { Name = "C", SortOrder = 3 });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!await dbContext.Subjects.AnyAsync(cancellationToken))
        {
            var systemsCareerId = await dbContext.Careers
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync(cancellationToken);
            dbContext.Subjects.AddRange(
                new Subject { Name = "Programación", Code = "PROG", CareerId = systemsCareerId },
                new Subject { Name = "Base de Datos", Code = "BD", CareerId = systemsCareerId },
                new Subject { Name = "Redes", Code = "RED", CareerId = systemsCareerId },
                new Subject { Name = "Teleprocesos", Code = "TEL", CareerId = systemsCareerId });
        }

        if (!await dbContext.ProjectTypes.AnyAsync(cancellationToken))
        {
            dbContext.ProjectTypes.AddRange(
                new ProjectType { Name = "Método", Description = "Propuesta metodológica o procedimiento." },
                new ProjectType { Name = "Prototipo a escala", Description = "Modelo funcional o demostrativo." },
                new ProjectType { Name = "Producto listo", Description = "Producto con un nivel avanzado de terminación." });
        }

        if (!await dbContext.SchoolCycles.AnyAsync(cancellationToken))
        {
            dbContext.SchoolCycles.Add(new SchoolCycle
            {
                Name = "Septiembre - Diciembre 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });
        }

        if (!await dbContext.ReviewRequirements.AnyAsync(cancellationToken))
        {
            dbContext.ReviewRequirements.AddRange(
                new ReviewRequirement { Name = "Nombre del proyecto", SortOrder = 1 },
                new ReviewRequirement { Name = "Planteamiento completo", SortOrder = 2 },
                new ReviewRequirement { Name = "Solución propuesta", SortOrder = 3 },
                new ReviewRequirement { Name = "Participantes", SortOrder = 4 },
                new ReviewRequirement { Name = "Materia", SortOrder = 5 },
                new ReviewRequirement { Name = "Documentación", Description = "Aplicará cuando el módulo de archivos esté habilitado.", SortOrder = 6 });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSuperAdministratorAsync()
    {
        var options = initialAdminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning(
                "No se creó el SuperAdministrador inicial. Configure Portal__SeedAdmin__Email y Portal__SeedAdmin__Password para la primera ejecución.");
            return;
        }

        var normalizedUserName = options.UserName.Trim().ToUpperInvariant();
        var existing = await userManager.FindByNameAsync(normalizedUserName);
        if (existing is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            FullName = options.FullName.Trim(),
            EnrollmentNumber = normalizedUserName,
            NormalizedEnrollmentNumber = normalizedUserName,
            UserName = normalizedUserName,
            Email = options.Email.Trim(),
            EmailConfirmed = true,
            AccountStatus = AccountStatus.Approved,
            ApprovedAtUtc = DateTimeOffset.UtcNow,
            MustChangePassword = true
        };

        var createResult = await userManager.CreateAsync(user, options.Password);
        EnsureSucceeded(createResult, "crear el SuperAdministrador inicial");
        var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.SuperAdministrator);
        EnsureSucceeded(roleResult, "asignar el rol SuperAdministrador");
        logger.LogWarning("Se creó el SuperAdministrador inicial {UserName}; debe cambiar su contraseña al iniciar sesión.", normalizedUserName);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"No fue posible {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }
    }
}
