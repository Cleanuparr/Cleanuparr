using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class RemoveSeekerTotalEligibleItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "total_eligible_items",
                schema: "data",
                table: "seeker_instance_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "total_eligible_items",
                schema: "data",
                table: "seeker_instance_configs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
