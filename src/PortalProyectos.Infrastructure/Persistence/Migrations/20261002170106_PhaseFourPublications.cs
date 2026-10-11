using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalProyectos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseFourPublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AltText",
                table: "MediaFiles",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCover",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProjectLikes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLikes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPublications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedProjectVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFeatured = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    HiddenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PublishedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPublications_AspNetUsers_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPublications_ProjectVersions_PublishedProjectVersionId",
                        column: x => x.PublishedProjectVersionId,
                        principalTable: "ProjectVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPublications_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLikes_ProjectId_UserId",
                table: "ProjectLikes",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLikes_UserId",
                table: "ProjectLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPublications_IsPublished_IsFeatured_PublishedAtUtc",
                table: "ProjectPublications",
                columns: new[] { "IsPublished", "IsFeatured", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPublications_ProjectId",
                table: "ProjectPublications",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPublications_PublishedByUserId",
                table: "ProjectPublications",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPublications_PublishedProjectVersionId",
                table: "ProjectPublications",
                column: "PublishedProjectVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectLikes");

            migrationBuilder.DropTable(
                name: "ProjectPublications");

            migrationBuilder.DropColumn(
                name: "AltText",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "IsCover",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "MediaFiles");
        }
    }
}
