using PortalProyectos.Application.Common;

namespace PortalProyectos.Application.Users;

public interface IStudentAccountService
{
    Task<RegistrationResult> RegisterAsync(RegisterStudentRequest request, CancellationToken cancellationToken = default);
    Task<StudentProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateProfileAsync(string userId, UpdateStudentProfileRequest request, CancellationToken cancellationToken = default);
}
