using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddDownloadClientHttpAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "http_auth_password",
                table: "download_clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "http_auth_type",
                table: "download_clients",
                type: "TEXT",
                nullable: false,
                defaultValue: "basic");

            migrationBuilder.AddColumn<string>(
                name: "http_auth_username",
                table: "download_clients",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "http_auth_password",
                table: "download_clients");

            migrationBuilder.DropColumn(
                name: "http_auth_type",
                table: "download_clients");

            migrationBuilder.DropColumn(
                name: "http_auth_username",
                table: "download_clients");
        }
    }
}
