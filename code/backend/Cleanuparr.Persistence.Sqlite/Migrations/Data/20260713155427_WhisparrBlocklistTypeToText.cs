using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class WhisparrBlocklistTypeToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "whisparr_blocklist_type",
                table: "content_blocker_configs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.Sql("UPDATE content_blocker_configs SET whisparr_blocklist_type = 'blacklist' WHERE whisparr_blocklist_type IN ('0', 0);");
            migrationBuilder.Sql("UPDATE content_blocker_configs SET whisparr_blocklist_type = 'whitelist' WHERE whisparr_blocklist_type IN ('1', 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE content_blocker_configs SET whisparr_blocklist_type = '0' WHERE whisparr_blocklist_type = 'blacklist';");
            migrationBuilder.Sql("UPDATE content_blocker_configs SET whisparr_blocklist_type = '1' WHERE whisparr_blocklist_type = 'whitelist';");

            migrationBuilder.AlterColumn<int>(
                name: "whisparr_blocklist_type",
                table: "content_blocker_configs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
