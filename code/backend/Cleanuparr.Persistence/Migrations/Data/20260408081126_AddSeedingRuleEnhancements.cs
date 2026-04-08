using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeedingRuleEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "categories",
                table: "u_torrent_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "u_torrent_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tracker_patterns",
                table: "u_torrent_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "categories",
                table: "transmission_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "transmission_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tags_all",
                table: "transmission_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "tags_any",
                table: "transmission_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "tracker_patterns",
                table: "transmission_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "categories",
                table: "r_torrent_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "r_torrent_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tracker_patterns",
                table: "r_torrent_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "categories",
                table: "q_bit_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "q_bit_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tags_all",
                table: "q_bit_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "tags_any",
                table: "q_bit_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "tracker_patterns",
                table: "q_bit_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "categories",
                table: "deluge_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "deluge_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "tracker_patterns",
                table: "deluge_seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            // Data migration: populate categories from name and assign sequential priorities per client
            foreach (var table in new[] { "q_bit_seeding_rules", "deluge_seeding_rules", "transmission_seeding_rules", "u_torrent_seeding_rules", "r_torrent_seeding_rules" })
            {
                // Populate categories from existing name (preserves existing rule matching behaviour)
                migrationBuilder.Sql($"UPDATE {table} SET categories = json_array(name)");

                // Assign sequential priorities per download client, ordered by rowid
                migrationBuilder.Sql($@"
                    UPDATE {table}
                    SET priority = (
                        SELECT COUNT(*)
                        FROM {table} r2
                        WHERE r2.download_client_config_id = {table}.download_client_config_id
                          AND r2.rowid <= {table}.rowid
                    )");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "categories",
                table: "u_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "u_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tracker_patterns",
                table: "u_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "categories",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tags_all",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tags_any",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tracker_patterns",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "categories",
                table: "r_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "r_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tracker_patterns",
                table: "r_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "categories",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tags_all",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tags_any",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tracker_patterns",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "categories",
                table: "deluge_seeding_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "deluge_seeding_rules");

            migrationBuilder.DropColumn(
                name: "tracker_patterns",
                table: "deluge_seeding_rules");
        }
    }
}
