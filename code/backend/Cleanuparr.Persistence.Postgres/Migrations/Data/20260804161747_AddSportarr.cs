using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSportarr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sportarr_blocklist_path",
                schema: "data",
                table: "content_blocker_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sportarr_blocklist_type",
                schema: "data",
                table: "content_blocker_configs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "sportarr_enabled",
                schema: "data",
                table: "content_blocker_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                schema: "data",
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { new Guid("3f6cd06a-98b4-45f1-8f0e-1c2d7a5b9e04"), (short)-1, "sportarr" });

            migrationBuilder.Sql("UPDATE data.content_blocker_configs SET sportarr_blocklist_type = 'blacklist' WHERE sportarr_blocklist_type = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sportarr_blocklist_path",
                schema: "data",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "sportarr_blocklist_type",
                schema: "data",
                table: "content_blocker_configs");

            migrationBuilder.DropColumn(
                name: "sportarr_enabled",
                schema: "data",
                table: "content_blocker_configs");
        }
    }
}
