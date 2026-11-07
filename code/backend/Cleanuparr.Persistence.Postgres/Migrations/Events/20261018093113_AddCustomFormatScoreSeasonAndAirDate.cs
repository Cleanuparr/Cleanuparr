using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddCustomFormatScoreSeasonAndAirDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "air_date_utc",
                schema: "events",
                table: "custom_format_score_entries",
                type: "timestamp with time zone",
                nullable: true);

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
                name: "air_date_utc",
                schema: "events",
                table: "custom_format_score_entries");

            migrationBuilder.DropColumn(
                name: "season_number",
                schema: "events",
                table: "custom_format_score_entries");
        }
    }
}
