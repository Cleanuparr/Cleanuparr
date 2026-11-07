using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeekerUnitRest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rest_max_days",
                schema: "data",
                table: "seeker_instance_configs",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "rest_units_enabled",
                schema: "data",
                table: "seeker_instance_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "rest_max_days",
                schema: "data",
                table: "seeker_instance_configs");

            migrationBuilder.DropColumn(
                name: "rest_units_enabled",
                schema: "data",
                table: "seeker_instance_configs");
        }
    }
}
