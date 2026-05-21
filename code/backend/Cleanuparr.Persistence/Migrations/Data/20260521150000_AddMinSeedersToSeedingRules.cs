using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    [DbContext(typeof(DataContext))]
    [Migration("20260521150000_AddMinSeedersToSeedingRules")]
    public partial class AddMinSeedersToSeedingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
                     {
                         "q_bit_seeding_rules",
                         "deluge_seeding_rules",
                         "transmission_seeding_rules",
                         "u_torrent_seeding_rules",
                         "r_torrent_seeding_rules"
                     })
            {
                migrationBuilder.AddColumn<int>(
                    name: "min_seeders",
                    table: table,
                    type: "INTEGER",
                    nullable: false,
                    defaultValue: -1);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
                     {
                         "q_bit_seeding_rules",
                         "deluge_seeding_rules",
                         "transmission_seeding_rules",
                         "u_torrent_seeding_rules",
                         "r_torrent_seeding_rules"
                     })
            {
                migrationBuilder.DropColumn(
                    name: "min_seeders",
                    table: table);
            }
        }
    }
}
