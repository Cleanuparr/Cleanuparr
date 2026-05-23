using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddOrphanedFilesCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_orphaned_files_to_process",
                table: "orphaned_files_cleaner_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_orphaned_files_to_process",
                table: "orphaned_files_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
