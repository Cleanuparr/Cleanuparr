using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddCustomFormatScoreSeasonAndAirDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "air_date_utc",
                table: "custom_format_score_entries",
                type: "TEXT",
                nullable: true);

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
                name: "air_date_utc",
                table: "custom_format_score_entries");

            migrationBuilder.DropColumn(
                name: "season_number",
                table: "custom_format_score_entries");
        }
    }
}
