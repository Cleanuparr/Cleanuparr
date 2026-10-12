using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddDownloadClientHttpAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "http_auth_password",
                schema: "data",
                table: "download_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "http_auth_type",
                schema: "data",
                table: "download_clients",
                type: "text",
                nullable: false,
                defaultValue: "basic");

            migrationBuilder.AddColumn<string>(
                name: "http_auth_username",
                schema: "data",
                table: "download_clients",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "http_auth_password",
                schema: "data",
                table: "download_clients");

            migrationBuilder.DropColumn(
                name: "http_auth_type",
                schema: "data",
                table: "download_clients");

            migrationBuilder.DropColumn(
                name: "http_auth_username",
                schema: "data",
                table: "download_clients");
        }
    }
}
