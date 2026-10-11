namespace PortalProyectos.Application.Users;

public sealed record RegisterStudentRequest(
    string FullName,
    string EnrollmentNumber,
    string Email,
    string Password,
    int CareerId,
    int GradeId,
    int AcademicGroupId);

public sealed record RegistrationResult(
    bool Succeeded,
    string Message,
    string? UserId = null,
    IReadOnlyList<string>? Errors = null);
