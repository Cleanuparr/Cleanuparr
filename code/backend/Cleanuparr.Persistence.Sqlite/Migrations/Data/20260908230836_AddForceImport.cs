using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddForceImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "failed_import_force_import",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<ushort>(
                name: "failed_import_force_import_max_tries",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)3);

            migrationBuilder.AddColumn<bool>(
                name: "on_force_imported",
                table: "notification_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_import_force_import",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "failed_import_force_import_max_tries",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "on_force_imported",
                table: "notification_configs");
        }
    }
}
