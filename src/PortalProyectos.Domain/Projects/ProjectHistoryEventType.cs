namespace PortalProyectos.Domain.Projects;

public enum ProjectHistoryEventType
{
    Created = 1,
    DraftUpdated = 2,
    ParticipantAdded = 3,
    ParticipantRemoved = 4,
    Submitted = 5,
    SubmissionCancelled = 6,
    ReviewStarted = 7,
    ChangesRequested = 8,
    Resubmitted = 9,
    Approved = 10,
    Rejected = 11,
    Deleted = 12
}
