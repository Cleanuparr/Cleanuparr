using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddFailedImportTypeHandling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "failed_import_ignored_patterns",
                table: "queue_cleaner_configs",
                newName: "failed_import_patterns");

            migrationBuilder.AddColumn<string>(
                name: "failed_import_pattern_mode",
                table: "queue_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE queue_cleaner_configs
                SET failed_import_pattern_mode = CASE
                    WHEN failed_import_max_strikes = 0 AND failed_import_patterns = '[]'
                        THEN 'include'
                    ELSE 'exclude'
                END;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_import_pattern_mode",
                table: "queue_cleaner_configs");

            migrationBuilder.RenameColumn(
                name: "failed_import_patterns",
                table: "queue_cleaner_configs",
                newName: "failed_import_ignored_patterns");
        }
    }
}
