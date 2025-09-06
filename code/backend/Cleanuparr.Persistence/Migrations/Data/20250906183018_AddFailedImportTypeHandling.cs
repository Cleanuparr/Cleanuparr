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
                defaultValue: "exclude");
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
