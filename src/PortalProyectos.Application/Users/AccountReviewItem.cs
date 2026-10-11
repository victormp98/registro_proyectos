using PortalProyectos.Domain.Users;

namespace PortalProyectos.Application.Users;

public sealed record AccountReviewItem(
    string Id,
    string FullName,
    string EnrollmentNumber,
    string Email,
    string Career,
    string Grade,
    string Group,
    AccountStatus Status,
    string? StatusReason,
    DateTimeOffset CreatedAtUtc);
