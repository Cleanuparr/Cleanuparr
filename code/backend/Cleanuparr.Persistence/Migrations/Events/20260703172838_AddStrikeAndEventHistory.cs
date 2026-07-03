using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddStrikeAndEventHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<string>(type: "TEXT", nullable: false),
                    event_type = table.Column<string>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    severity = table.Column<string>(type: "TEXT", nullable: false),
                    archived_at = table.Column<string>(type: "TEXT", nullable: false),
                    tracking_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    strike_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    job_run_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    download_client_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    search_status = table.Column<string>(type: "TEXT", nullable: true),
                    completed_at = table.Column<string>(type: "TEXT", nullable: true),
                    cycle_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    is_dry_run = table.Column<bool>(type: "INTEGER", nullable: false),
                    item_title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    item_hash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    strike_count = table.Column<int>(type: "INTEGER", nullable: true),
                    failed_import_reasons = table.Column<string>(type: "TEXT", nullable: false),
                    delete_reason = table.Column<string>(type: "TEXT", nullable: true),
                    remove_from_client = table.Column<bool>(type: "INTEGER", nullable: true),
                    clean_reason = table.Column<string>(type: "TEXT", nullable: true),
                    cleaned_category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    seed_ratio = table.Column<double>(type: "REAL", nullable: true),
                    seeding_time_hours = table.Column<double>(type: "REAL", nullable: true),
                    old_category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    new_category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    is_category_tag = table.Column<bool>(type: "INTEGER", nullable: true),
                    search_type = table.Column<string>(type: "TEXT", nullable: true),
                    search_reason = table.Column<string>(type: "TEXT", nullable: true),
                    grabbed_items = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "strike_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_item_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    item_title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    item_hash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    outcome = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    struck_at = table.Column<string>(type: "TEXT", nullable: false),
                    archived_at = table.Column<string>(type: "TEXT", nullable: false),
                    job_run_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_downloaded_bytes = table.Column<long>(type: "INTEGER", nullable: true),
                    is_dry_run = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strike_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_history_archived_at",
                table: "event_history",
                column: "archived_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_event_history_delete_reason",
                table: "event_history",
                column: "delete_reason");

            migrationBuilder.CreateIndex(
                name: "ix_event_history_event_type",
                table: "event_history",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_event_history_severity",
                table: "event_history",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_event_history_timestamp",
                table: "event_history",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_strike_history_archived_at",
                table: "strike_history",
                column: "archived_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_strike_history_outcome",
                table: "strike_history",
                column: "outcome");

            migrationBuilder.CreateIndex(
                name: "ix_strike_history_type",
                table: "strike_history",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_history");

            migrationBuilder.DropTable(
                name: "strike_history");
        }
    }
}
