using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using PortalProyectos.Domain.Catalogs;
using PortalProyectos.Domain.Users;

namespace PortalProyectos.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(160)]
    public required string FullName { get; set; }

    [MaxLength(32)]
    public required string EnrollmentNumber { get; set; }

    [MaxLength(32)]
    public required string NormalizedEnrollmentNumber { get; set; }

    public AccountStatus AccountStatus { get; set; } = AccountStatus.Pending;

    public AccountStatus? StatusBeforeBlock { get; set; }

    [MaxLength(500)]
    public string? StatusReason { get; set; }

    public int? CareerId { get; set; }
    public Career? Career { get; set; }

    public int? GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int? AcademicGroupId { get; set; }
    public AcademicGroup? AcademicGroup { get; set; }

    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ApprovedByUserId { get; set; }
}
