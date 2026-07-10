using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddReadarr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "readarr_blocklist_path",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "readarr_blocklist_type",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "readarr_enabled",
                table: "content_blocker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
            
            migrationBuilder.InsertData(
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { new Guid("013994ea-0a5e-4eed-91b7-271f494b6259"), (short)-1, "readarr" });

            migrationBuilder.Sql("UPDATE content_blocker_configs SET readarr_blocklist_type = 'blacklist' WHERE readarr_blocklist_type = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "readarr_blocklist_path",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "readarr_blocklist_type",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "readarr_enabled",
                table: "content_blocker_configs");
        }
    }
}
