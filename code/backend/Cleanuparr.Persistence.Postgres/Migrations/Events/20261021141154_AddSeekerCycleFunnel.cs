using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSeekerCycleFunnel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "funnel_counted_at",
                schema: "events",
                table: "seeker_cycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "library_items",
                schema: "events",
                table: "seeker_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "monitored_items",
                schema: "events",
                table: "seeker_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "queued_units",
                schema: "events",
                table: "seeker_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "recently_grabbed_units",
                schema: "events",
                table: "seeker_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "released_items",
                schema: "events",
                table: "seeker_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tag_filtered_items",
                schema: "events",
                table: "seeker_cycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "funnel_counted_at",
                schema: "events",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "library_items",
                schema: "events",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "monitored_items",
                schema: "events",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "queued_units",
                schema: "events",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "recently_grabbed_units",
                schema: "events",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "released_items",
                schema: "events",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "tag_filtered_items",
                schema: "events",
                table: "seeker_cycles");
        }
    }
}
