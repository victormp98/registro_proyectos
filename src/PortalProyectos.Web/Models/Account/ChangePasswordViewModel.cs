using System.ComponentModel.DataAnnotations;

namespace PortalProyectos.Web.Models.Account;

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Escribe tu contraseña temporal o actual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe una contraseña nueva.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres.")]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la contraseña nueva.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
