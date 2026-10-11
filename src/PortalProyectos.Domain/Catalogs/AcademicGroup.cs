using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Domain.Catalogs;

public sealed class AcademicGroup
{
    public int Id { get; set; }

    [MaxLength(40)]
    public required string Name { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
