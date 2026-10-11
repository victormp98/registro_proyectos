using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PortalProyectos.Web.Validation;

namespace PortalProyectos.Web.Models.Account;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Escribe tu nombre completo.")]
    [StringLength(160, MinimumLength = 5, ErrorMessage = "El nombre debe tener entre 5 y 160 caracteres.")]
    [Display(Name = "Nombre completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu matrícula.")]
    [StringLength(32, MinimumLength = 3, ErrorMessage = "La matrícula debe tener entre 3 y 32 caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9-]+$", ErrorMessage = "Usa únicamente letras, números y guiones.")]
    [Display(Name = "Matrícula")]
    public string EnrollmentNumber { get; set; } = string.Empty;

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

    [Required(ErrorMessage = "Crea una contraseña.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres.")]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma tu contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [MustBeTrue]
    public bool AcceptPrivacy { get; set; }

    public IReadOnlyList<SelectListItem> Careers { get; set; } = [];
    public IReadOnlyList<SelectListItem> Grades { get; set; } = [];
    public IReadOnlyList<SelectListItem> Groups { get; set; } = [];
}
