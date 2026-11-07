using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeekerQuietHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "quiet_hours_enabled",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_hours_end",
                table: "seeker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(7, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_hours_start",
                table: "seeker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(23, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "quiet_hours_enabled",
                table: "seeker_configs");

            migrationBuilder.DropColumn(
                name: "quiet_hours_end",
                table: "seeker_configs");

            migrationBuilder.DropColumn(
                name: "quiet_hours_start",
                table: "seeker_configs");
        }
    }
}
