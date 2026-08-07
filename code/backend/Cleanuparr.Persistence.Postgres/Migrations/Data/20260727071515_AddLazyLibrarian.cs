using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddLazyLibrarian : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lazy_librarian_blocklist_path",
                schema: "data",
                table: "content_blocker_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lazy_librarian_blocklist_type",
                schema: "data",
                table: "content_blocker_configs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "lazy_librarian_enabled",
                schema: "data",
                table: "content_blocker_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                schema: "data",
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { new Guid("d7c2a7a5-6f31-4f0d-9c52-0c2b8c5b1a90"), (short)-1, "lazylibrarian" });

            migrationBuilder.Sql("UPDATE data.content_blocker_configs SET lazy_librarian_blocklist_type = 'blacklist' WHERE lazy_librarian_blocklist_type = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "data",
                table: "arr_configs",
                keyColumn: "id",
                keyValue: new Guid("d7c2a7a5-6f31-4f0d-9c52-0c2b8c5b1a90"));

            migrationBuilder.DropColumn(
                name: "lazy_librarian_blocklist_path",
                schema: "data",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "lazy_librarian_blocklist_type",
                schema: "data",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "lazy_librarian_enabled",
                schema: "data",
                table: "content_blocker_configs");
        }
    }
}
