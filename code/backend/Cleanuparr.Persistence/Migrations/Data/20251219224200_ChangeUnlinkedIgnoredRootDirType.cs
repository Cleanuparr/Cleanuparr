using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class ChangeUnlinkedIgnoredRootDirType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "unlinked_ignored_root_dir",
                table: "download_cleaner_configs",
                newName: "unlinked_ignored_root_dirs");

            migrationBuilder.Sql("""
                UPDATE download_cleaner_configs
                SET unlinked_ignored_root_dirs = CASE
                    WHEN unlinked_ignored_root_dirs IS NULL OR unlinked_ignored_root_dirs = '' THEN '[]'
                    ELSE '["' || unlinked_ignored_root_dirs || '"]'
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE download_cleaner_configs
                SET unlinked_ignored_root_dirs = CASE
                    WHEN unlinked_ignored_root_dirs = '[]' OR unlinked_ignored_root_dirs IS NULL THEN ''
                    ELSE SUBSTR(unlinked_ignored_root_dirs, 3, LENGTH(unlinked_ignored_root_dirs) - 4)
                END
                """);

            migrationBuilder.RenameColumn(
                name: "unlinked_ignored_root_dirs",
                table: "download_cleaner_configs",
                newName: "unlinked_ignored_root_dir");
        }
    }
}
