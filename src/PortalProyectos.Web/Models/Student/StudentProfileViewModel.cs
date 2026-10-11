using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Domain.Users;

namespace PortalProyectos.Web.Models.Student;

public sealed class StudentProfileViewModel
{
    public string EnrollmentNumber { get; set; } = string.Empty;
    public AccountStatus Status { get; set; }
    public string? StatusReason { get; set; }

    [Required(ErrorMessage = "Escribe tu nombre completo.")]
    [StringLength(160, MinimumLength = 5, ErrorMessage = "El nombre debe tener entre 5 y 160 caracteres.")]
    [Display(Name = "Nombre completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(256)]
    [Display(Name = "Correo institucional")]
    public string Email { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una carrera.")]
    [Display(Name = "Carrera")]
    public int CareerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un grado.")]
    [Display(Name = "Grado")]
    public int GradeId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un grupo.")]
    [Display(Name = "Grupo")]
    public int AcademicGroupId { get; set; }

    public IReadOnlyList<SelectListItem> Careers { get; set; } = [];
    public IReadOnlyList<SelectListItem> Grades { get; set; } = [];
    public IReadOnlyList<SelectListItem> Groups { get; set; } = [];
}
