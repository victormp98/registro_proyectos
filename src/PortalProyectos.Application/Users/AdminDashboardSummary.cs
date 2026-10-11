namespace PortalProyectos.Application.Users;

public sealed record AdminDashboardSummary(
    int PendingUsers,
    int ApprovedStudents,
    int RejectedUsers,
    int BlockedUsers,
    int ActiveCareers,
    int ProjectsWaiting,
    int ProjectsUnderReview,
    int ApprovedProjects,
    int PublishedProjects,
    int HiddenProjects,
    int FeaturedProjects,
    int ProjectLikes,
    int TotalStudents,
    int TotalProjects,
    int ProjectsWithCorrections,
    int CompletedProjects,
    int PublishedNews,
    int TotalParticipants,
    IReadOnlyList<DashboardBreakdown> ProjectsByCareer,
    IReadOnlyList<DashboardBreakdown> ProjectsBySubject,
    IReadOnlyList<DashboardBreakdown> ProjectsByCycle,
    IReadOnlyList<DashboardBreakdown> ProjectsByStatus);

public sealed record DashboardBreakdown(string Label, int Count);
