using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSearchItemGrabbedNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "item_type",
                table: "seeker_command_trackers");

            migrationBuilder.AddColumn<bool>(
                name: "on_search_item_grabbed",
                table: "notification_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "on_search_item_grabbed",
                table: "notification_configs");

            migrationBuilder.AddColumn<string>(
                name: "item_type",
                table: "seeker_command_trackers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
