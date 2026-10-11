namespace PortalProyectos.Infrastructure.Configuration;

public sealed class ProjectOptions
{
    public const string SectionName = "Portal:Projects";
    public int MaxParticipants { get; set; } = 6;
    public int MaxActiveProjects { get; set; } = 3;
}
