using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class NormalizeDownloadHashCasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // download_id is now written lowercase, so rows stored in another casing became unreachable.
            // The unique index is case-sensitive, so the same torrent can already sit in several rows.
            migrationBuilder.DropIndex(
                name: "ix_download_items_download_id",
                table: "download_items");

            // Repoint every loser's strikes onto the survivor before anything is deleted:
            // strikes.download_item_id cascades on delete, so the reverse order would destroy them.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT
        id,
        lower(download_id) AS normalized,
        ROW_NUMBER() OVER (
            PARTITION BY lower(download_id)
            ORDER BY id
        ) AS rank_in_group
    FROM download_items
),
survivors AS (
    SELECT normalized, id AS survivor_id
    FROM ranked
    WHERE rank_in_group = 1
),
losers AS (
    SELECT r.id AS loser_id, s.survivor_id
    FROM ranked r
    JOIN survivors s ON s.normalized = r.normalized
    WHERE r.rank_in_group > 1
)
UPDATE strikes
SET download_item_id = (
    SELECT survivor_id FROM losers WHERE losers.loser_id = strikes.download_item_id
)
WHERE download_item_id IN (SELECT loser_id FROM losers);
");

            // OR the group's status flags onto the survivor, which keeps its own title.
            migrationBuilder.Sql(@"
WITH grouped AS (
    SELECT
        lower(download_id) AS normalized,
        MAX(is_marked_for_removal) AS is_marked_for_removal,
        MAX(is_removed) AS is_removed,
        MAX(is_returning) AS is_returning
    FROM download_items
    GROUP BY lower(download_id)
)
UPDATE download_items
SET
    is_marked_for_removal = (
        SELECT is_marked_for_removal FROM grouped WHERE grouped.normalized = lower(download_items.download_id)
    ),
    is_removed = (
        SELECT is_removed FROM grouped WHERE grouped.normalized = lower(download_items.download_id)
    ),
    is_returning = (
        SELECT is_returning FROM grouped WHERE grouped.normalized = lower(download_items.download_id)
    );
");

            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT
        id,
        ROW_NUMBER() OVER (
            PARTITION BY lower(download_id)
            ORDER BY id
        ) AS rank_in_group
    FROM download_items
)
DELETE FROM download_items
WHERE id IN (SELECT id FROM ranked WHERE rank_in_group > 1);
");

            migrationBuilder.Sql("UPDATE download_items SET download_id = lower(download_id);");

            // events.item_hash carries no unique index, so it needs no merge.
            // manual_events.item_hash is left alone: it is normalized in code and its unique index is filtered.
            migrationBuilder.Sql("UPDATE events SET item_hash = lower(item_hash) WHERE item_hash IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_download_items_download_id",
                table: "download_items",
                column: "download_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the merge cannot be unmerged, and the index is identical before and after Up.
        }
    }
}
