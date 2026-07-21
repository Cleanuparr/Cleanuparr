using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class MoveSeekerFiltersToInstanceConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new columns to seeker_instance_configs with sensible defaults
            migrationBuilder.AddColumn<bool>(
                name: "monitored_only",
                table: "seeker_instance_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "use_cutoff",
                table: "seeker_instance_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_custom_format_score",
                table: "seeker_instance_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Step 2: Copy current global values to all existing instance configs
            migrationBuilder.Sql("""
                UPDATE seeker_instance_configs
                SET monitored_only = (SELECT monitored_only FROM seeker_configs LIMIT 1),
                    use_cutoff = (SELECT use_cutoff FROM seeker_configs LIMIT 1),
                    use_custom_format_score = (SELECT use_custom_format_score FROM seeker_configs LIMIT 1)
                WHERE EXISTS (SELECT 1 FROM seeker_configs)
                """);

            // Step 3: Drop columns from global seeker_configs
            migrationBuilder.DropColumn(
                name: "monitored_only",
                table: "seeker_configs");

            migrationBuilder.DropColumn(
                name: "use_cutoff",
                table: "seeker_configs");

            migrationBuilder.DropColumn(
                name: "use_custom_format_score",
                table: "seeker_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add columns to global seeker_configs
            migrationBuilder.AddColumn<bool>(
                name: "monitored_only",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "use_cutoff",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_custom_format_score",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Copy values from first instance config back to global
            migrationBuilder.Sql("""
                UPDATE seeker_configs
                SET monitored_only = COALESCE((SELECT monitored_only FROM seeker_instance_configs LIMIT 1), 1),
                    use_cutoff = COALESCE((SELECT use_cutoff FROM seeker_instance_configs LIMIT 1), 0),
                    use_custom_format_score = COALESCE((SELECT use_custom_format_score FROM seeker_instance_configs LIMIT 1), 0)
                """);

            // Drop columns from instance configs
            migrationBuilder.DropColumn(
                name: "monitored_only",
                table: "seeker_instance_configs");

            migrationBuilder.DropColumn(
                name: "use_cutoff",
                table: "seeker_instance_configs");

            migrationBuilder.DropColumn(
                name: "use_custom_format_score",
                table: "seeker_instance_configs");
        }
    }
}
