using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddMaxInactiveDaysToQBitSeedingRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "max_inactive_days",
                table: "q_bit_seeding_rules",
                type: "REAL",
                nullable: false,
                defaultValue: -1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_inactive_days",
                table: "q_bit_seeding_rules");
        }
    }
}
