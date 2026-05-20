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
            migrationBuilder.CreateTable(
                name: "orphaned_files_cleaner_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    cron_expression = table.Column<string>(type: "TEXT", nullable: false),
                    use_advanced_scheduling = table.Column<bool>(type: "INTEGER", nullable: false),
                    scan_directories = table.Column<string>(type: "TEXT", nullable: false),
                    orphaned_directory = table.Column<string>(type: "TEXT", nullable: true),
                    download_directory_source = table.Column<string>(type: "TEXT", nullable: true),
                    download_directory_target = table.Column<string>(type: "TEXT", nullable: true),
                    exclude_patterns = table.Column<string>(type: "TEXT", nullable: false),
                    min_file_age_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    max_orphaned_files_to_process = table.Column<int>(type: "INTEGER", nullable: false),
                    empty_after_x_days = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orphaned_files_cleaner_configs", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "orphaned_files_cleaner_configs",
                columns: new[] { "id", "enabled", "cron_expression", "use_advanced_scheduling", "scan_directories", "orphaned_directory", "download_directory_source", "download_directory_target", "exclude_patterns", "min_file_age_minutes", "max_orphaned_files_to_process", "empty_after_x_days" },
                values: new object[] { Guid.NewGuid(), false, "0 0 * * * ?", false, "[]", null, null, null, "[]", 0, 50, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orphaned_files_cleaner_configs");
        }
    }
}
