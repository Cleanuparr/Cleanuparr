using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddAppriseCliMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "apprise_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "api");

            migrationBuilder.AddColumn<string>(
                name: "service_urls",
                table: "apprise_configs",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mode",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "service_urls",
                table: "apprise_configs");
        }
    }
}
