using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSeekerCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_season_number_cycle_id",
                table: "seeker_history");

            migrationBuilder.AddColumn<long>(
                name: "episode_id",
                table: "seeker_history",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "grabs",
                table: "seeker_history",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_outcome",
                table: "seeker_history",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seeker_cycles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    started_at = table.Column<string>(type: "TEXT", nullable: false),
                    completed_at = table.Column<string>(type: "TEXT", nullable: true),
                    completion_reason = table.Column<string>(type: "TEXT", nullable: true),
                    eligible_at_start = table.Column<int>(type: "INTEGER", nullable: false),
                    eligible_now = table.Column<int>(type: "INTEGER", nullable: false),
                    remaining_units = table.Column<int>(type: "INTEGER", nullable: false),
                    searched_units = table.Column<int>(type: "INTEGER", nullable: false),
                    grabbed_units = table.Column<int>(type: "INTEGER", nullable: false),
                    is_dry_run = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_cycles", x => x.id);
                });

            // History becomes one row per unit, so a unit's rows from past cycles fold into its newest one.
            migrationBuilder.Sql(@"
UPDATE seeker_history
SET search_count = (
    SELECT SUM(h2.search_count) FROM seeker_history h2
    WHERE h2.arr_instance_id = seeker_history.arr_instance_id AND h2.external_item_id = seeker_history.external_item_id AND h2.item_type = seeker_history.item_type AND h2.season_number = seeker_history.season_number AND h2.is_dry_run = seeker_history.is_dry_run)
WHERE NOT EXISTS (
    SELECT 1 FROM seeker_history h2
    WHERE h2.arr_instance_id = seeker_history.arr_instance_id AND h2.external_item_id = seeker_history.external_item_id AND h2.item_type = seeker_history.item_type AND h2.season_number = seeker_history.season_number AND h2.is_dry_run = seeker_history.is_dry_run
      AND (h2.last_searched_at > seeker_history.last_searched_at
           OR (h2.last_searched_at = seeker_history.last_searched_at AND h2.id > seeker_history.id)));");

            migrationBuilder.Sql(@"
DELETE FROM seeker_history
WHERE EXISTS (
    SELECT 1 FROM seeker_history h2
    WHERE h2.arr_instance_id = seeker_history.arr_instance_id AND h2.external_item_id = seeker_history.external_item_id AND h2.item_type = seeker_history.item_type AND h2.season_number = seeker_history.season_number AND h2.is_dry_run = seeker_history.is_dry_run
      AND (h2.last_searched_at > seeker_history.last_searched_at
           OR (h2.last_searched_at = seeker_history.last_searched_at AND h2.id > seeker_history.id)));");

            // Past cycles exist only as a cycle id on search events.
            // The newest cycle of each instance stays open, Seeker reconciles it with the live cycle id on its next run.
            migrationBuilder.Sql(@"
INSERT INTO seeker_cycles (id, arr_instance_id, started_at, completed_at, completion_reason, eligible_at_start, eligible_now, remaining_units, searched_units, grabbed_units, is_dry_run)
SELECT e.cycle_id, e.arr_instance_id, MIN(e.timestamp), MAX(e.timestamp), 'completed', COUNT(*), 0, 0, COUNT(*),
       SUM(CASE WHEN e.grabbed_items <> '[]' THEN 1 ELSE 0 END), MIN(e.is_dry_run)
FROM events e
WHERE e.event_type = 'searchtriggered' AND e.cycle_id IS NOT NULL AND e.arr_instance_id IS NOT NULL
GROUP BY e.cycle_id, e.arr_instance_id;");

            migrationBuilder.Sql(@"
UPDATE seeker_cycles
SET completed_at = NULL, completion_reason = NULL
WHERE NOT EXISTS (
    SELECT 1 FROM seeker_cycles c2
    WHERE c2.arr_instance_id = seeker_cycles.arr_instance_id
      AND c2.is_dry_run = seeker_cycles.is_dry_run
      AND c2.started_at > seeker_cycles.started_at);");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_season_number_episode_id_is_dry_run",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type", "season_number", "episode_id", "is_dry_run" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seeker_cycles_arr_instance_id",
                table: "seeker_cycles",
                column: "arr_instance_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seeker_cycles");

            migrationBuilder.DropIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_season_number_episode_id_is_dry_run",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "episode_id",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "grabs",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "last_outcome",
                table: "seeker_history");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_season_number_cycle_id",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type", "season_number", "cycle_id" },
                unique: true);
        }
    }
}
