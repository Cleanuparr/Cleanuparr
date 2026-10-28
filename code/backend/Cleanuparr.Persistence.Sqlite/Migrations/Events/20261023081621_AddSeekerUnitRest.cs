using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSeekerUnitRest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "consecutive_misses",
                table: "seeker_history",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_miss_at",
                table: "seeker_history",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resting_units",
                table: "seeker_cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consecutive_misses",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "last_miss_at",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "resting_units",
                table: "seeker_cycles");
        }
    }
}
