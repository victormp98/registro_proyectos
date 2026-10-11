using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalProyectos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseFourPublicationParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParticipantsSnapshot",
                table: "ProjectPublications",
                type: "TEXT",
                maxLength: 20000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParticipantsSnapshot",
                table: "ProjectPublications");
        }
    }
}
