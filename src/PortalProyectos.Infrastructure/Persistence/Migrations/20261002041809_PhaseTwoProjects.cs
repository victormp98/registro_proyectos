using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalProyectos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseTwoProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false, collation: "NOCASE"),
                    Code = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true, collation: "NOCASE"),
                    CareerId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subjects_Careers_CareerId",
                        column: x => x.CareerId,
                        principalTable: "Careers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHistoryEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectVersionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectReviewId = table.Column<int>(type: "INTEGER", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    ActorDisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 800, nullable: true),
                    IsVisibleToStudent = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHistoryEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    StudentNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, collation: "NOCASE"),
                    CareerId = table.Column<int>(type: "INTEGER", nullable: false),
                    GradeId = table.Column<int>(type: "INTEGER", nullable: false),
                    AcademicGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsResponsible = table.Column<bool>(type: "INTEGER", nullable: false),
                    LinkConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectParticipants_AcademicGroups_AcademicGroupId",
                        column: x => x.AcademicGroupId,
                        principalTable: "AcademicGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectParticipants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectParticipants_Careers_CareerId",
                        column: x => x.CareerId,
                        principalTable: "Careers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectParticipants_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReviewRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectReviewId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewRequirementId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsMet = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReviewRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReviewRequirements_ReviewRequirements_ReviewRequirementId",
                        column: x => x.ReviewRequirementId,
                        principalTable: "ReviewRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    AdministratorId = table.Column<string>(type: "TEXT", nullable: true),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReviews_AspNetUsers_AdministratorId",
                        column: x => x.AdministratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResponsibleUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CurrentVersionId = table.Column<int>(type: "INTEGER", nullable: true),
                    LatestApprovedVersionId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedAdministratorId = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewStartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_AspNetUsers_AssignedAdministratorId",
                        column: x => x.AssignedAdministratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_AspNetUsers_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SubjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    SchoolCycleId = table.Column<int>(type: "INTEGER", nullable: true),
                    DevelopmentStatus = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    ProblemStatement = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: false),
                    ProposedSolution = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: false),
                    Advisor = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    EstimatedCompletionDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_ProjectTypes_ProjectTypeId",
                        column: x => x.ProjectTypeId,
                        principalTable: "ProjectTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_SchoolCycles_SchoolCycleId",
                        column: x => x.SchoolCycleId,
                        principalTable: "SchoolCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHistoryEvents_ProjectId_CreatedAtUtc",
                table: "ProjectHistoryEvents",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHistoryEvents_ProjectReviewId",
                table: "ProjectHistoryEvents",
                column: "ProjectReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHistoryEvents_ProjectVersionId",
                table: "ProjectHistoryEvents",
                column: "ProjectVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_AcademicGroupId",
                table: "ProjectParticipants",
                column: "AcademicGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_CareerId",
                table: "ProjectParticipants",
                column: "CareerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_GradeId",
                table: "ProjectParticipants",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_OneResponsible",
                table: "ProjectParticipants",
                column: "ProjectId",
                unique: true,
                filter: "\"IsResponsible\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_ProjectId_StudentNumber",
                table: "ProjectParticipants",
                columns: new[] { "ProjectId", "StudentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_ProjectId_UserId",
                table: "ProjectParticipants",
                columns: new[] { "ProjectId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParticipants_UserId",
                table: "ProjectParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReviewRequirements_ProjectReviewId_ReviewRequirementId",
                table: "ProjectReviewRequirements",
                columns: new[] { "ProjectReviewId", "ReviewRequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReviewRequirements_ReviewRequirementId",
                table: "ProjectReviewRequirements",
                column: "ReviewRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReviews_AdministratorId",
                table: "ProjectReviews",
                column: "AdministratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReviews_ProjectId_ReviewNumber",
                table: "ProjectReviews",
                columns: new[] { "ProjectId", "ReviewNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReviews_ProjectVersionId",
                table: "ProjectReviews",
                column: "ProjectVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AssignedAdministratorId",
                table: "Projects",
                column: "AssignedAdministratorId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CurrentVersionId",
                table: "Projects",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_LatestApprovedVersionId",
                table: "Projects",
                column: "LatestApprovedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ResponsibleUserId_CurrentStatus",
                table: "Projects",
                columns: new[] { "ResponsibleUserId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTypes_Name",
                table: "ProjectTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectId_VersionNumber",
                table: "ProjectVersions",
                columns: new[] { "ProjectId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectTypeId",
                table: "ProjectVersions",
                column: "ProjectTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_SchoolCycleId",
                table: "ProjectVersions",
                column: "SchoolCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_SubjectId",
                table: "ProjectVersions",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequirements_Name",
                table: "ReviewRequirements",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolCycles_Name",
                table: "SchoolCycles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CareerId_Code",
                table: "Subjects",
                columns: new[] { "CareerId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CareerId_Name",
                table: "Subjects",
                columns: new[] { "CareerId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectHistoryEvents_ProjectReviews_ProjectReviewId",
                table: "ProjectHistoryEvents",
                column: "ProjectReviewId",
                principalTable: "ProjectReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectHistoryEvents_ProjectVersions_ProjectVersionId",
                table: "ProjectHistoryEvents",
                column: "ProjectVersionId",
                principalTable: "ProjectVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectHistoryEvents_Projects_ProjectId",
                table: "ProjectHistoryEvents",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectParticipants_Projects_ProjectId",
                table: "ProjectParticipants",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectReviewRequirements_ProjectReviews_ProjectReviewId",
                table: "ProjectReviewRequirements",
                column: "ProjectReviewId",
                principalTable: "ProjectReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectReviews_ProjectVersions_ProjectVersionId",
                table: "ProjectReviews",
                column: "ProjectVersionId",
                principalTable: "ProjectVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectReviews_Projects_ProjectId",
                table: "ProjectReviews",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectVersions_CurrentVersionId",
                table: "Projects",
                column: "CurrentVersionId",
                principalTable: "ProjectVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectVersions_LatestApprovedVersionId",
                table: "Projects",
                column: "LatestApprovedVersionId",
                principalTable: "ProjectVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectVersions_CurrentVersionId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectVersions_LatestApprovedVersionId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectHistoryEvents");

            migrationBuilder.DropTable(
                name: "ProjectParticipants");

            migrationBuilder.DropTable(
                name: "ProjectReviewRequirements");

            migrationBuilder.DropTable(
                name: "ProjectReviews");

            migrationBuilder.DropTable(
                name: "ReviewRequirements");

            migrationBuilder.DropTable(
                name: "ProjectVersions");

            migrationBuilder.DropTable(
                name: "ProjectTypes");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "SchoolCycles");

            migrationBuilder.DropTable(
                name: "Subjects");
        }
    }
}
