using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class UnifySearchEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new columns to seeker_configs
            migrationBuilder.AddColumn<bool>(
                name: "proactive_search_enabled",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "search_enabled_new",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<ushort>(
                name: "search_interval",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)2);

            // 2. Migrate data:
            //    - Old seeker_configs.enabled → new proactive_search_enabled
            //    - Old general_configs.search_enabled → new seeker_configs.search_enabled
            //    - Old general_configs.search_delay (seconds) → new search_interval (minutes), clamped to 1-10
            migrationBuilder.Sql(
                "UPDATE seeker_configs SET proactive_search_enabled = enabled");

            migrationBuilder.Sql(@"
                UPDATE seeker_configs SET search_enabled_new = (
                    SELECT COALESCE(g.search_enabled, 1) FROM general_configs g LIMIT 1
                )");

            migrationBuilder.Sql(@"
                UPDATE seeker_configs SET search_interval = (
                    SELECT CASE
                        WHEN g.search_delay <= 60 THEN 1
                        WHEN g.search_delay >= 600 THEN 10
                        ELSE MAX(1, MIN(10, ROUND(CAST(g.search_delay AS REAL) / 60.0)))
                    END
                    FROM general_configs g LIMIT 1
                )");

            // 3. Drop old columns from seeker_configs
            migrationBuilder.DropColumn(
                name: "enabled",
                table: "seeker_configs");

            migrationBuilder.DropColumn(
                name: "cron_expression",
                table: "seeker_configs");

            migrationBuilder.DropColumn(
                name: "use_advanced_scheduling",
                table: "seeker_configs");

            // 4. Rename temp column to final name
            migrationBuilder.RenameColumn(
                name: "search_enabled_new",
                table: "seeker_configs",
                newName: "search_enabled");

            // 5. Drop old columns from general_configs
            migrationBuilder.DropColumn(
                name: "search_delay",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "search_enabled",
                table: "general_configs");

            // 6. Create search_queue table
            migrationBuilder.CreateTable(
                name: "search_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    series_id = table.Column<long>(type: "INTEGER", nullable: true),
                    search_type = table.Column<string>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_queue", x => x.id);
                    table.ForeignKey(
                        name: "fk_search_queue_arr_instances_arr_instance_id",
                        column: x => x.arr_instance_id,
                        principalTable: "arr_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_search_queue_arr_instance_id",
                table: "search_queue",
                column: "arr_instance_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_queue");

            // Restore old seeker_configs columns
            migrationBuilder.AddColumn<bool>(
                name: "enabled",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "cron_expression",
                table: "seeker_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "use_advanced_scheduling",
                table: "seeker_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Migrate data back
            migrationBuilder.Sql(
                "UPDATE seeker_configs SET enabled = proactive_search_enabled");

            // Drop new seeker_configs columns
            migrationBuilder.DropColumn(
                name: "proactive_search_enabled",
                table: "seeker_configs");

            migrationBuilder.DropColumn(
                name: "search_interval",
                table: "seeker_configs");

            // Rename search_enabled back
            migrationBuilder.RenameColumn(
                name: "search_enabled",
                table: "seeker_configs",
                newName: "search_enabled_temp");

            // Restore old general_configs columns
            migrationBuilder.AddColumn<ushort>(
                name: "search_delay",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)120);

            migrationBuilder.AddColumn<bool>(
                name: "search_enabled",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            // Copy search_enabled back to general_configs
            migrationBuilder.Sql(@"
                UPDATE general_configs SET search_enabled = (
                    SELECT COALESCE(s.search_enabled_temp, 1) FROM seeker_configs s LIMIT 1
                )");

            migrationBuilder.DropColumn(
                name: "search_enabled_temp",
                table: "seeker_configs");
        }
    }
}
