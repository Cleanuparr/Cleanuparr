using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddAdvancedLoggingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "log_archive_enabled",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<ushort>(
                name: "log_archive_retained_count",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<ushort>(
                name: "log_archive_time_limit_hours",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<ushort>(
                name: "log_retained_file_count",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<ushort>(
                name: "log_rolling_size_mb",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<ushort>(
                name: "log_time_limit_hours",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);
            
            migrationBuilder.Sql(
                "UPDATE general_configs SET log_archive_enabled = 1, log_archive_retained_count = 60, log_archive_time_limit_hours = 720, log_retained_file_count = 5, log_rolling_size_mb = 10, log_time_limit_hours = 24"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "log_archive_enabled",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "log_archive_retained_count",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "log_archive_time_limit_hours",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "log_retained_file_count",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "log_rolling_size_mb",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "log_time_limit_hours",
                table: "general_configs");
        }
    }
}
