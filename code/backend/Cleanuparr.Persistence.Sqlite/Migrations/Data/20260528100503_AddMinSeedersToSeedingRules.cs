using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddMinSeedersToSeedingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "min_seeders",
                table: "u_torrent_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "min_seeders",
                table: "transmission_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "min_seeders",
                table: "q_bit_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "min_seeders",
                table: "deluge_seeding_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "min_seeders",
                table: "u_torrent_seeding_rules");

            migrationBuilder.DropColumn(
                name: "min_seeders",
                table: "transmission_seeding_rules");

            migrationBuilder.DropColumn(
                name: "min_seeders",
                table: "q_bit_seeding_rules");

            migrationBuilder.DropColumn(
                name: "min_seeders",
                table: "deluge_seeding_rules");
        }
    }
}
