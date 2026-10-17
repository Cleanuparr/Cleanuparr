using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSeekerCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_s",
                schema: "events",
                table: "seeker_history");

            migrationBuilder.AddColumn<long>(
                name: "episode_id",
                schema: "events",
                table: "seeker_history",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "grabs",
                schema: "events",
                table: "seeker_history",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_outcome",
                schema: "events",
                table: "seeker_history",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seeker_cycles",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completion_reason = table.Column<string>(type: "text", nullable: true),
                    eligible_at_start = table.Column<int>(type: "integer", nullable: false),
                    eligible_now = table.Column<int>(type: "integer", nullable: false),
                    remaining_units = table.Column<int>(type: "integer", nullable: false),
                    searched_units = table.Column<int>(type: "integer", nullable: false),
                    grabbed_units = table.Column<int>(type: "integer", nullable: false),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_cycles", x => x.id);
                });

            // History becomes one row per unit, so a unit's rows from past cycles fold into its newest one.
            migrationBuilder.Sql(@"
UPDATE events.seeker_history h
SET search_count = (
    SELECT SUM(h2.search_count) FROM events.seeker_history h2
    WHERE h2.arr_instance_id = h.arr_instance_id AND h2.external_item_id = h.external_item_id AND h2.item_type = h.item_type AND h2.season_number = h.season_number AND h2.is_dry_run = h.is_dry_run)
WHERE NOT EXISTS (
    SELECT 1 FROM events.seeker_history h2
    WHERE h2.arr_instance_id = h.arr_instance_id AND h2.external_item_id = h.external_item_id AND h2.item_type = h.item_type AND h2.season_number = h.season_number AND h2.is_dry_run = h.is_dry_run
      AND (h2.last_searched_at > h.last_searched_at
           OR (h2.last_searched_at = h.last_searched_at AND h2.id > h.id)));");

            migrationBuilder.Sql(@"
DELETE FROM events.seeker_history h
WHERE EXISTS (
    SELECT 1 FROM events.seeker_history h2
    WHERE h2.arr_instance_id = h.arr_instance_id AND h2.external_item_id = h.external_item_id AND h2.item_type = h.item_type AND h2.season_number = h.season_number AND h2.is_dry_run = h.is_dry_run
      AND (h2.last_searched_at > h.last_searched_at
           OR (h2.last_searched_at = h.last_searched_at AND h2.id > h.id)));");

            // Past cycles exist only as a cycle id on search events.
            // The newest cycle of each instance stays open, Seeker reconciles it with the live cycle id on its next run.
            migrationBuilder.Sql(@"
INSERT INTO events.seeker_cycles (id, arr_instance_id, started_at, completed_at, completion_reason, eligible_at_start, eligible_now, remaining_units, searched_units, grabbed_units, is_dry_run)
SELECT e.cycle_id, e.arr_instance_id, MIN(e.timestamp), MAX(e.timestamp), 'completed', COUNT(*), 0, 0, COUNT(*),
       SUM(CASE WHEN cardinality(e.grabbed_items) > 0 THEN 1 ELSE 0 END), MIN(e.is_dry_run::int) = 1
FROM events.events e
WHERE e.event_type = 'searchtriggered' AND e.cycle_id IS NOT NULL AND e.arr_instance_id IS NOT NULL
GROUP BY e.cycle_id, e.arr_instance_id;");

            migrationBuilder.Sql(@"
UPDATE events.seeker_cycles c
SET completed_at = NULL, completion_reason = NULL
WHERE NOT EXISTS (
    SELECT 1 FROM events.seeker_cycles c2
    WHERE c2.arr_instance_id = c.arr_instance_id
      AND c2.is_dry_run = c.is_dry_run
      AND c2.started_at > c.started_at);");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_s",
                schema: "events",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type", "season_number", "episode_id", "is_dry_run" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seeker_cycles_arr_instance_id",
                schema: "events",
                table: "seeker_cycles",
                column: "arr_instance_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seeker_cycles",
                schema: "events");

            migrationBuilder.DropIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_s",
                schema: "events",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "episode_id",
                schema: "events",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "grabs",
                schema: "events",
                table: "seeker_history");

            migrationBuilder.DropColumn(
                name: "last_outcome",
                schema: "events",
                table: "seeker_history");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_s",
                schema: "events",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type", "season_number", "cycle_id" },
                unique: true);
        }
    }
}
