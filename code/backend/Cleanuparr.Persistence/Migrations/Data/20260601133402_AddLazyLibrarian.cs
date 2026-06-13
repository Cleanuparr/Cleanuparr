using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddLazyLibrarian : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lazy_librarian_blocklist_path",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lazy_librarian_blocklist_type",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "lazy_librarian_enabled",
                table: "content_blocker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { new Guid("d7c2a7a5-6f31-4f0d-9c52-0c2b8c5b1a90"), (short)-1, "lazylibrarian" });

            migrationBuilder.Sql("UPDATE content_blocker_configs SET lazy_librarian_blocklist_type = 'blacklist' WHERE lazy_librarian_blocklist_type = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "arr_configs",
                keyColumn: "id",
                keyValue: new Guid("d7c2a7a5-6f31-4f0d-9c52-0c2b8c5b1a90"));

            migrationBuilder.DropColumn(
                name: "lazy_librarian_blocklist_path",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "lazy_librarian_blocklist_type",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "lazy_librarian_enabled",
                table: "content_blocker_configs");
        }
    }
}
