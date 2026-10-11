using System.ComponentModel.DataAnnotations;
using PortalProyectos.Domain.Users;

namespace PortalProyectos.Web.Models.Admin;

public sealed class AdministrativeUserForm
{
    [Required, StringLength(160, MinimumLength = 3)] public string FullName { get; set; } = string.Empty;
    [Required, StringLength(32, MinimumLength = 3), RegularExpression("^[A-Za-z0-9-]+$")] public string EmployeeNumber { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
}

public sealed record AdministrativeUserItem(string Id, string FullName, string EmployeeNumber, string Email, AccountStatus Status, DateTimeOffset CreatedAtUtc);
