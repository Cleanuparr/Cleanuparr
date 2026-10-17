using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddCustomFormatScoreSeasonNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "season_number",
                schema: "events",
                table: "custom_format_score_entries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "season_number",
                schema: "events",
                table: "custom_format_score_entries");
        }
    }
}
