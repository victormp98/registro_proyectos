using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortalProyectos.Domain.Auditing;
using PortalProyectos.Domain.Catalogs;
using PortalProyectos.Domain.Projects;
using PortalProyectos.Domain.Notifications;
using PortalProyectos.Domain.Files;
using PortalProyectos.Domain.News;
using PortalProyectos.Infrastructure.Identity;

namespace PortalProyectos.Infrastructure.Persistence;

public sealed class PortalDbContext(DbContextOptions<PortalDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Career> Careers => Set<Career>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<AcademicGroup> AcademicGroups => Set<AcademicGroup>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SchoolCycle> SchoolCycles => Set<SchoolCycle>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<ReviewRequirement> ReviewRequirements => Set<ReviewRequirement>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();
    public DbSet<ProjectParticipant> ProjectParticipants => Set<ProjectParticipant>();
    public DbSet<ProjectReview> ProjectReviews => Set<ProjectReview>();
    public DbSet<ProjectReviewRequirement> ProjectReviewRequirements => Set<ProjectReviewRequirement>();
    public DbSet<ProjectHistoryEvent> ProjectHistoryEvents => Set<ProjectHistoryEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<ProjectPublication> ProjectPublications => Set<ProjectPublication>();
    public DbSet<ProjectLike> ProjectLikes => Set<ProjectLike>();
    public DbSet<NewsPost> NewsPosts => Set<NewsPost>();
    public DbSet<NewsExternalLink> NewsExternalLinks => Set<NewsExternalLink>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(x => x.NormalizedEnrollmentNumber).IsUnique();
            entity.Property(x => x.AccountStatus).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.StatusBeforeBlock).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(x => x.Career).WithMany().HasForeignKey(x => x.CareerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcademicGroup).WithMany().HasForeignKey(x => x.AcademicGroupId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Career>(entity =>
        {
            entity.Property(x => x.Name).UseCollation("NOCASE");
            entity.Property(x => x.Code).UseCollation("NOCASE");
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<Grade>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<AcademicGroup>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<AuditLog>().HasIndex(x => x.CreatedAtUtc);

        builder.Entity<Subject>(entity =>
        {
            entity.Property(x => x.Name).UseCollation("NOCASE");
            entity.Property(x => x.Code).UseCollation("NOCASE");
            entity.HasIndex(x => new { x.CareerId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.CareerId, x.Code }).IsUnique();
            entity.HasOne(x => x.Career).WithMany().HasForeignKey(x => x.CareerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SchoolCycle>(entity =>
        {
            entity.Property(x => x.Name).UseCollation("NOCASE");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<ProjectType>(entity =>
        {
            entity.Property(x => x.Name).UseCollation("NOCASE");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<ReviewRequirement>(entity =>
        {
            entity.Property(x => x.Name).UseCollation("NOCASE");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Project>(entity =>
        {
            entity.Property(x => x.CurrentStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.ResponsibleUserId, x.CurrentStatus });
            entity.HasIndex(x => x.AssignedAdministratorId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedAdministratorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectVersion>().WithMany().HasForeignKey(x => x.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectVersion>().WithMany().HasForeignKey(x => x.LatestApprovedVersionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectVersion>(entity =>
        {
            entity.Property(x => x.DevelopmentStatus).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => new { x.ProjectId, x.VersionNumber }).IsUnique();
            entity.HasOne(x => x.Project).WithMany(x => x.Versions).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProjectType).WithMany().HasForeignKey(x => x.ProjectTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SchoolCycle).WithMany().HasForeignKey(x => x.SchoolCycleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectParticipant>(entity =>
        {
            entity.Property(x => x.StudentNumber).UseCollation("NOCASE");
            entity.HasIndex(x => new { x.ProjectId, x.StudentNumber }).IsUnique();
            entity.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            entity.HasIndex(x => x.ProjectId).IsUnique().HasFilter("\"IsResponsible\" = 1").HasDatabaseName("IX_ProjectParticipants_OneResponsible");
            entity.HasOne(x => x.Project).WithMany(x => x.Participants).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Career).WithMany().HasForeignKey(x => x.CareerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AcademicGroup).WithMany().HasForeignKey(x => x.AcademicGroupId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectReview>(entity =>
        {
            entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.ProjectId, x.ReviewNumber }).IsUnique();
            entity.HasIndex(x => x.ProjectVersionId);
            entity.HasOne(x => x.Project).WithMany(x => x.Reviews).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ProjectVersion).WithMany().HasForeignKey(x => x.ProjectVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectReviewRequirement>(entity =>
        {
            entity.HasIndex(x => new { x.ProjectReviewId, x.ReviewRequirementId }).IsUnique();
            entity.HasOne(x => x.ProjectReview).WithMany(x => x.Requirements).HasForeignKey(x => x.ProjectReviewId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ReviewRequirement).WithMany().HasForeignKey(x => x.ReviewRequirementId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectHistoryEvent>(entity =>
        {
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
            entity.HasOne(x => x.Project).WithMany(x => x.History).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProjectVersion>().WithMany().HasForeignKey(x => x.ProjectVersionId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<ProjectReview>().WithMany().HasForeignKey(x => x.ProjectReviewId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
            entity.HasIndex(x => x.DeduplicationKey).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MediaFile>(entity =>
        {
            entity.Property(x => x.FileType).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.ProjectVersionId, x.FileType, x.IsDeleted });
            entity.HasIndex(x => new { x.ProjectVersionId, x.FileType }).IsUnique().HasFilter("\"IsDeleted\" = 0");
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
            entity.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectVersion>().WithMany().HasForeignKey(x => x.ProjectVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.NewsPost).WithMany().HasForeignKey(x => x.NewsPostId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NewsPost>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Title).UseCollation("NOCASE");
            entity.HasIndex(x => new { x.Status, x.IsFeatured, x.Id });
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NewsExternalLink>(entity =>
        {
            entity.HasIndex(x => new { x.NewsPostId, x.SortOrder });
            entity.HasOne(x => x.NewsPost).WithMany(x => x.Links).HasForeignKey(x => x.NewsPostId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MediaFile>().HasIndex(x => new { x.NewsPostId, x.FileType }).IsUnique()
            .HasFilter("\"NewsPostId\" IS NOT NULL AND \"FileType\" = 'NewsVideo' AND \"IsDeleted\" = 0");

        builder.Entity<ProjectPublication>(entity =>
        {
            entity.HasIndex(x => x.ProjectId).IsUnique();
            entity.HasIndex(x => new { x.IsPublished, x.IsFeatured, x.PublishedAtUtc });
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProjectVersion>().WithMany().HasForeignKey(x => x.PublishedProjectVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectLike>(entity =>
        {
            entity.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.Project).WithMany(x => x.ProjectLikes).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
