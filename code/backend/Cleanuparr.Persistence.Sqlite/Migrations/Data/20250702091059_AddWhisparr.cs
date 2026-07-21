using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddWhisparr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "whisparr_blocklist_path",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "whisparr_blocklist_type",
                table: "content_blocker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "whisparr_enabled",
                table: "content_blocker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
            
            migrationBuilder.InsertData(
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { new Guid("a7363ca9-224a-46c1-9a94-edda11fde7b2"), (short)-1, "whisparr" });

            migrationBuilder.Sql("UPDATE content_blocker_configs SET whisparr_blocklist_type = 'blacklist' WHERE whisparr_blocklist_type = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "whisparr_blocklist_path",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "whisparr_blocklist_type",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "whisparr_enabled",
                table: "content_blocker_configs");
        }
    }
}
