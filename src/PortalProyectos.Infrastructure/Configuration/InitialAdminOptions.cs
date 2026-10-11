namespace PortalProyectos.Infrastructure.Configuration;

public sealed class InitialAdminOptions
{
    public const string SectionName = "Portal:SeedAdmin";

    public string FullName { get; set; } = "Superadministrador inicial";
    public string UserName { get; set; } = "SUPERADMIN";
    public string? Email { get; set; }
    public string? Password { get; set; }
}
