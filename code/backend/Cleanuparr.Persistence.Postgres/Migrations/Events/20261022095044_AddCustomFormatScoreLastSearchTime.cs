using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddCustomFormatScoreLastSearchTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_search_time",
                schema: "events",
                table: "custom_format_score_entries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_search_time",
                schema: "events",
                table: "custom_format_score_entries");
        }
    }
}
