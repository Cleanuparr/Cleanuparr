using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSeekerCycleFunnel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "funnel_counted_at",
                table: "seeker_cycles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "library_items",
                table: "seeker_cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "monitored_items",
                table: "seeker_cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "queued_units",
                table: "seeker_cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "recently_grabbed_units",
                table: "seeker_cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "released_items",
                table: "seeker_cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tag_filtered_items",
                table: "seeker_cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "funnel_counted_at",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "library_items",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "monitored_items",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "queued_units",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "recently_grabbed_units",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "released_items",
                table: "seeker_cycles");

            migrationBuilder.DropColumn(
                name: "tag_filtered_items",
                table: "seeker_cycles");
        }
    }
}
