using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSeekerContentKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_kind",
                table: "seeker_history",
                type: "TEXT",
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.AddColumn<string>(
                name: "content_kind",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE seeker_history
SET content_kind = 'special'
WHERE item_type = 'sonarr' AND season_number = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_kind",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "content_kind",
                table: "events");
        }
    }
}
