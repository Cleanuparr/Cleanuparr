using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSeeker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cycle_run_id",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_status",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_cycle_run_id",
                table: "events",
                column: "cycle_run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_events_cycle_run_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "cycle_run_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "search_status",
                table: "events");
        }
    }
}
