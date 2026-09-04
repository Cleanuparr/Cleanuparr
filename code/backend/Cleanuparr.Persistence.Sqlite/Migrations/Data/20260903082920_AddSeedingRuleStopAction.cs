using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeedingRuleStopAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "u_torrent_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "transmission_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "r_torrent_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "q_bit_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "delete");

            migrationBuilder.AddColumn<bool>(
                name: "on_download_stopped",
                table: "notification_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "deluge_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "delete");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "action",
                table: "u_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "action",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "action",
                table: "r_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "action",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "on_download_stopped",
                table: "notification_configs");

            migrationBuilder.DropColumn(
                name: "action",
                table: "deluge_seeding_rules");
        }
    }
}
