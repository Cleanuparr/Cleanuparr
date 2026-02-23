using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddAuthConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "auth_disable_auth_for_local_addresses",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "auth_trust_forwarded_headers",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "auth_trusted_networks",
                table: "general_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auth_disable_auth_for_local_addresses",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_trust_forwarded_headers",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_trusted_networks",
                table: "general_configs");
        }
    }
}
