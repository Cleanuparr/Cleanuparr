using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class MovePathRemappingToDownloadClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "download_directory_source",
                table: "download_clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_directory_target",
                table: "download_clients",
                type: "TEXT",
                nullable: true);

            // Migrate existing path remapping values from unlinked_configs to download_clients.
            // Each download client has at most one unlinked config; we take the first non-null pair found.
            migrationBuilder.Sql(@"
                UPDATE download_clients
                SET
                    download_directory_source = (
                        SELECT download_directory_source
                        FROM unlinked_configs
                        WHERE unlinked_configs.download_client_config_id = download_clients.id
                          AND download_directory_source IS NOT NULL
                        LIMIT 1
                    ),
                    download_directory_target = (
                        SELECT download_directory_target
                        FROM unlinked_configs
                        WHERE unlinked_configs.download_client_config_id = download_clients.id
                          AND download_directory_target IS NOT NULL
                        LIMIT 1
                    )
                WHERE EXISTS (
                    SELECT 1 FROM unlinked_configs
                    WHERE unlinked_configs.download_client_config_id = download_clients.id
                      AND download_directory_source IS NOT NULL
                )
            ");

            migrationBuilder.DropColumn(
                name: "download_directory_source",
                table: "unlinked_configs");

            migrationBuilder.DropColumn(
                name: "download_directory_target",
                table: "unlinked_configs");

            migrationBuilder.DropColumn(
                name: "download_directory_source",
                table: "orphaned_files_client_configs");

            migrationBuilder.DropColumn(
                name: "download_directory_target",
                table: "orphaned_files_client_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "download_directory_source",
                table: "unlinked_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_directory_target",
                table: "unlinked_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_directory_source",
                table: "orphaned_files_client_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_directory_target",
                table: "orphaned_files_client_configs",
                type: "TEXT",
                nullable: true);

            // Restore path remapping values from download_clients back to unlinked_configs
            migrationBuilder.Sql(@"
                UPDATE unlinked_configs
                SET
                    download_directory_source = (
                        SELECT download_directory_source FROM download_clients
                        WHERE download_clients.id = unlinked_configs.download_client_config_id
                    ),
                    download_directory_target = (
                        SELECT download_directory_target FROM download_clients
                        WHERE download_clients.id = unlinked_configs.download_client_config_id
                    )
            ");

            migrationBuilder.DropColumn(
                name: "download_directory_source",
                table: "download_clients");

            migrationBuilder.DropColumn(
                name: "download_directory_target",
                table: "download_clients");
        }
    }
}
