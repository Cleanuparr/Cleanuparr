using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddCustomFormatScoreSeasonNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "season_number",
                table: "custom_format_score_entries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "season_number",
                table: "custom_format_score_entries");
        }
    }
}
