using PortalProyectos.Domain.Catalogs;

namespace PortalProyectos.Domain.Projects;

public sealed class ProjectReviewRequirement
{
    public int Id { get; set; }
    public int ProjectReviewId { get; set; }
    public ProjectReview? ProjectReview { get; set; }
    public int ReviewRequirementId { get; set; }
    public ReviewRequirement? ReviewRequirement { get; set; }
    public bool IsMet { get; set; }
}
