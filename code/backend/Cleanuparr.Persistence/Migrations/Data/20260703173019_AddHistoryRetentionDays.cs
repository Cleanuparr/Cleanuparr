using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddHistoryRetentionDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ushort>(
                name: "history_retention_days",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)365);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "history_retention_days",
                table: "general_configs");
        }
    }
}
