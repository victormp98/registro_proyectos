namespace PortalProyectos.Domain.Security;

public static class SystemRoles
{
    public const string Student = "Alumno";
    public const string Administrator = "Administrador";
    public const string SuperAdministrator = "SuperAdministrador";

    public const string AdministrativeRoles = Administrator + "," + SuperAdministrator;

    public static readonly string[] All = [Student, Administrator, SuperAdministrator];
}
