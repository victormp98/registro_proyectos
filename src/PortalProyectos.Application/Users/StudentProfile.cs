using PortalProyectos.Domain.Users;

namespace PortalProyectos.Application.Users;

public sealed record StudentProfile(
    string Id,
    string FullName,
    string EnrollmentNumber,
    string Email,
    int CareerId,
    int GradeId,
    int AcademicGroupId,
    string Career,
    string Grade,
    string Group,
    AccountStatus Status,
    string? StatusReason,
    DateTimeOffset CreatedAtUtc);

public sealed record UpdateStudentProfileRequest(
    string FullName,
    string Email,
    int CareerId,
    int GradeId,
    int AcademicGroupId);
