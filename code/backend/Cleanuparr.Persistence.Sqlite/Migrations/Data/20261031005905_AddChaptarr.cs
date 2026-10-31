using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddChaptarr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chaptarr_blocklist_path",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chaptarr_blocklist_type",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "chaptarr_enabled",
                table: "content_blocker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { new Guid("8156909c-64f4-4115-b36a-2e95b6f79384"), (short)-1, "chaptarr" });

            migrationBuilder.Sql("UPDATE content_blocker_configs SET chaptarr_blocklist_type = 'blacklist' WHERE chaptarr_blocklist_type = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "arr_configs",
                keyColumn: "id",
                keyValue: new Guid("8156909c-64f4-4115-b36a-2e95b6f79384"));

            migrationBuilder.DropColumn(
                name: "chaptarr_blocklist_path",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "chaptarr_blocklist_type",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "chaptarr_enabled",
                table: "content_blocker_configs");
        }
    }
}
