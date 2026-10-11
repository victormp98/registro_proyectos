using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Web.Models.Admin;

public sealed class CareerFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Escribe el nombre de la carrera.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(24)]
    [RegularExpression(@"^[A-Za-z0-9-]*$", ErrorMessage = "La clave solo admite letras, números y guiones.")]
    [Display(Name = "Clave")]
    public string? Code { get; set; }

    [Display(Name = "Disponible para registro")]
    public bool IsActive { get; set; } = true;
}
