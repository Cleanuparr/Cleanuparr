using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeedingRuleStopAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "action",
                schema: "data",
                table: "u_torrent_seeding_rules",
                type: "text",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<string>(
                name: "action",
                schema: "data",
                table: "transmission_seeding_rules",
                type: "text",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<string>(
                name: "action",
                schema: "data",
                table: "r_torrent_seeding_rules",
                type: "text",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<string>(
                name: "action",
                schema: "data",
                table: "q_bit_seeding_rules",
                type: "text",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<bool>(
                name: "on_download_stopped",
                schema: "data",
                table: "notification_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "action",
                schema: "data",
                table: "deluge_seeding_rules",
                type: "text",
                nullable: false,
                defaultValue: "delete");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "action",
                schema: "data",
                table: "u_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "action",
                schema: "data",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "action",
                schema: "data",
                table: "r_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "action",
                schema: "data",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "on_download_stopped",
                schema: "data",
                table: "notification_configs");

            migrationBuilder.DropColumn(
                name: "action",
                schema: "data",
                table: "deluge_seeding_rules");
        }
    }
}
