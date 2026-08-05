using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSportarr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sportarr_blocklist_path",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sportarr_blocklist_type",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "sportarr_enabled",
                table: "content_blocker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { new Guid("3f6cd06a-98b4-45f1-8f0e-1c2d7a5b9e04"), (short)-1, "sportarr" });

            migrationBuilder.Sql("UPDATE content_blocker_configs SET sportarr_blocklist_type = 'blacklist' WHERE sportarr_blocklist_type = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sportarr_blocklist_path",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "sportarr_blocklist_type",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "sportarr_enabled",
                table: "content_blocker_configs");
        }
    }
}
