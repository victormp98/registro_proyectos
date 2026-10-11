using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Web.Models.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Escribe tu matrícula o correo.")]
    [Display(Name = "Matrícula o correo institucional")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Mantener sesión iniciada")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
