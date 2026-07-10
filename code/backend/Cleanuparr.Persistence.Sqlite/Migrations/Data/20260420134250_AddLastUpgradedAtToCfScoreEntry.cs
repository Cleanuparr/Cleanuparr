using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddLastUpgradedAtToCfScoreEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_upgraded_at",
                table: "custom_format_score_entries",
                type: "TEXT",
                nullable: true);

            // Backfill last_upgraded_at from existing history: per item, the most recent
            // recorded_at at which the score strictly exceeded the preceding score.
            migrationBuilder.Sql(@"
WITH scored AS (
    SELECT
        arr_instance_id,
        external_item_id,
        episode_id,
        score,
        recorded_at,
        LAG(score) OVER (
            PARTITION BY arr_instance_id, external_item_id, episode_id
            ORDER BY recorded_at
        ) AS prev_score
    FROM custom_format_score_history
),
upgrades AS (
    SELECT
        arr_instance_id,
        external_item_id,
        episode_id,
        MAX(recorded_at) AS last_upgraded_at
    FROM scored
    WHERE prev_score IS NOT NULL AND score > prev_score
    GROUP BY arr_instance_id, external_item_id, episode_id
)
UPDATE custom_format_score_entries
SET last_upgraded_at = (
    SELECT last_upgraded_at FROM upgrades u
    WHERE u.arr_instance_id = custom_format_score_entries.arr_instance_id
      AND u.external_item_id = custom_format_score_entries.external_item_id
      AND u.episode_id = custom_format_score_entries.episode_id
);
");

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_entries_last_upgraded_at",
                table: "custom_format_score_entries",
                column: "last_upgraded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_custom_format_score_entries_last_upgraded_at",
                table: "custom_format_score_entries");

            migrationBuilder.DropColumn(
                name: "last_upgraded_at",
                table: "custom_format_score_entries");
        }
    }
}
