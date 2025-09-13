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

            migrationBuilder.Sql(
                """
                UPDATE queue_cleaner_configs
                SET ignored_downloads = (SELECT ignored_downloads FROM general_configs)
                WHERE (SELECT ignored_downloads FROM general_configs) != '';
                """);
            
            migrationBuilder.Sql(
                """
                UPDATE download_cleaner_configs
                SET ignored_downloads = (SELECT ignored_downloads FROM general_configs)
                WHERE (SELECT ignored_downloads FROM general_configs) != '';
                """);
            
            migrationBuilder.Sql(
                """
                UPDATE content_blocker_configs
                SET ignored_downloads = (SELECT ignored_downloads FROM general_configs)
                WHERE (SELECT ignored_downloads FROM general_configs) != '';
                """);
            
            migrationBuilder.DropColumn(
                name: "ignored_downloads",
                table: "general_configs");
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

            migrationBuilder.AddColumn<string>(
                name: "ignored_downloads",
                table: "general_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
