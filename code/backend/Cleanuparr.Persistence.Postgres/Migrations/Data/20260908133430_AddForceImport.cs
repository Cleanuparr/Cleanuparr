using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddForceImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "failed_import_force_import",
                schema: "data",
                table: "queue_cleaner_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_force_imported",
                schema: "data",
                table: "notification_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_import_force_import",
                schema: "data",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "on_force_imported",
                schema: "data",
                table: "notification_configs");
        }
    }
}
