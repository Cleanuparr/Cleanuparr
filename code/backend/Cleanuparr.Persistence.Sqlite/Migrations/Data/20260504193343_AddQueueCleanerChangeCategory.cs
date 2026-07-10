using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddQueueCleanerChangeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "change_category",
                table: "stall_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "change_category",
                table: "slow_rules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "failed_import_change_category",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "change_category",
                table: "stall_rules");

            migrationBuilder.DropColumn(
                name: "change_category",
                table: "slow_rules");

            migrationBuilder.DropColumn(
                name: "failed_import_change_category",
                table: "queue_cleaner_configs");
        }
    }
}
