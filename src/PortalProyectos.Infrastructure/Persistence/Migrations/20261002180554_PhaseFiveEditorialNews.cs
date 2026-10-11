using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalProyectos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseFiveEditorialNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewsPostId",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewsPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false, collation: "NOCASE"),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 12000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    IsFeatured = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    HiddenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExternalVideoProvider = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true),
                    ExternalVideoId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsPosts_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NewsExternalLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NewsPostId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsExternalLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsExternalLinks_NewsPosts_NewsPostId",
                        column: x => x.NewsPostId,
                        principalTable: "NewsPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_NewsPostId_FileType",
                table: "MediaFiles",
                columns: new[] { "NewsPostId", "FileType" },
                unique: true,
                filter: "\"NewsPostId\" IS NOT NULL AND \"FileType\" = 'NewsVideo' AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NewsExternalLinks_NewsPostId_SortOrder",
                table: "NewsExternalLinks",
                columns: new[] { "NewsPostId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_CreatedByUserId",
                table: "NewsPosts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsPosts_Status_IsFeatured_Id",
                table: "NewsPosts",
                columns: new[] { "Status", "IsFeatured", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_MediaFiles_NewsPosts_NewsPostId",
                table: "MediaFiles",
                column: "NewsPostId",
                principalTable: "NewsPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaFiles_NewsPosts_NewsPostId",
                table: "MediaFiles");

            migrationBuilder.DropTable(
                name: "NewsExternalLinks");

            migrationBuilder.DropTable(
                name: "NewsPosts");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_NewsPostId_FileType",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "NewsPostId",
                table: "MediaFiles");
        }
    }
}
