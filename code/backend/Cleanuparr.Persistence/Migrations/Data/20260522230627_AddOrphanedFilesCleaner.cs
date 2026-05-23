using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddOrphanedFilesCleaner : Migration
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

            // Migrate existing path remapping values from unlinked_configs to download_clients
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

            migrationBuilder.CreateTable(
                name: "orphaned_files_cleaner_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    exclude_patterns = table.Column<string>(type: "TEXT", nullable: false),
                    min_file_age_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    max_orphaned_files_to_process = table.Column<int>(type: "INTEGER", nullable: false),
                    empty_after_x_days = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orphaned_files_cleaner_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orphaned_files_client_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    scan_directories = table.Column<string>(type: "TEXT", nullable: false),
                    orphaned_directory = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orphaned_files_client_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_orphaned_files_client_configs_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orphaned_files_client_configs_download_client_config_id",
                table: "orphaned_files_client_configs",
                column: "download_client_config_id",
                unique: true);

            migrationBuilder.InsertData(
                table: "orphaned_files_cleaner_configs",
                columns: new[] { "id", "exclude_patterns", "min_file_age_minutes", "max_orphaned_files_to_process", "empty_after_x_days" },
                values: new object[] { Guid.NewGuid(), "[]", 0, 50, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orphaned_files_client_configs");

            migrationBuilder.DropTable(
                name: "orphaned_files_cleaner_configs");

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

            // Restore data from download_clients back to unlinked_configs
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
