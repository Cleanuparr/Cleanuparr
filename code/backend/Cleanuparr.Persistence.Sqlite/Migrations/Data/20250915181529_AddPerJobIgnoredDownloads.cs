using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddPerJobIgnoredDownloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ignored_downloads",
                table: "queue_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ignored_downloads",
                table: "download_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ignored_downloads",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ignored_downloads",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "ignored_downloads",
                table: "download_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "ignored_downloads",
                table: "content_blocker_configs");
        }
    }
}
