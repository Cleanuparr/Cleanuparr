using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddConnectivityCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "connectivity_check_enabled",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "connectivity_check_urls",
                table: "general_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "connectivity_check_enabled",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "connectivity_check_urls",
                table: "general_configs");
        }
    }
}
