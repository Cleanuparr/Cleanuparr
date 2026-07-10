using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Events
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "events");

            migrationBuilder.CreateTable(
                name: "custom_format_score_entries",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_item_id = table.Column<long>(type: "bigint", nullable: false),
                    episode_id = table.Column<long>(type: "bigint", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    file_id = table.Column<long>(type: "bigint", nullable: false),
                    current_score = table.Column<int>(type: "integer", nullable: false),
                    cutoff_score = table.Column<int>(type: "integer", nullable: false),
                    quality_profile_name = table.Column<string>(type: "text", nullable: false),
                    is_monitored = table.Column<bool>(type: "boolean", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_upgraded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_format_score_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custom_format_score_history",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_item_id = table.Column<long>(type: "bigint", nullable: false),
                    episode_id = table.Column<long>(type: "bigint", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    cutoff_score = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_format_score_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "download_items",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_marked_for_removal = table.Column<bool>(type: "boolean", nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false),
                    is_returning = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_download_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_runs",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_queue",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    series_id = table.Column<long>(type: "bigint", nullable: true),
                    search_type = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_queue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seeker_command_trackers",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_id = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_item_id = table.Column<long>(type: "bigint", nullable: false),
                    item_title = table.Column<string>(type: "text", nullable: false),
                    season_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_command_trackers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seeker_history",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_item_id = table.Column<long>(type: "bigint", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    season_number = table.Column<int>(type: "integer", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_searched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    item_title = table.Column<string>(type: "text", nullable: false),
                    search_count = table.Column<int>(type: "integer", nullable: false),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "manual_events",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    item_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    item_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    strike_count = table.Column<int>(type: "integer", nullable: true),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    job_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instance_type = table.Column<string>(type: "text", nullable: true),
                    instance_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    download_client_type = table.Column<string>(type: "text", nullable: true),
                    download_client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manual_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_manual_events_job_runs_job_run_id",
                        column: x => x.job_run_id,
                        principalSchema: "events",
                        principalTable: "job_runs",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "strikes",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_downloaded_bytes = table.Column<long>(type: "bigint", nullable: true),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strikes", x => x.id);
                    table.ForeignKey(
                        name: "fk_strikes_download_items_download_item_id",
                        column: x => x.download_item_id,
                        principalSchema: "events",
                        principalTable: "download_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_strikes_job_runs_job_run_id",
                        column: x => x.job_run_id,
                        principalSchema: "events",
                        principalTable: "job_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    tracking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    strike_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    download_client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    search_status = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    item_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    item_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    strike_count = table.Column<int>(type: "integer", nullable: true),
                    failed_import_reasons = table.Column<List<string>>(type: "text[]", nullable: false),
                    delete_reason = table.Column<string>(type: "text", nullable: true),
                    remove_from_client = table.Column<bool>(type: "boolean", nullable: true),
                    clean_reason = table.Column<string>(type: "text", nullable: true),
                    cleaned_category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    seed_ratio = table.Column<double>(type: "double precision", nullable: true),
                    seeding_time_hours = table.Column<double>(type: "double precision", nullable: true),
                    old_category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    new_category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_category_tag = table.Column<bool>(type: "boolean", nullable: true),
                    search_type = table.Column<string>(type: "text", nullable: true),
                    search_reason = table.Column<string>(type: "text", nullable: true),
                    grabbed_items = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_events_job_runs_job_run_id",
                        column: x => x.job_run_id,
                        principalSchema: "events",
                        principalTable: "job_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_events_strikes_strike_id",
                        column: x => x.strike_id,
                        principalSchema: "events",
                        principalTable: "strikes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_entries_arr_instance_id_external_item_i",
                schema: "events",
                table: "custom_format_score_entries",
                columns: new[] { "arr_instance_id", "external_item_id", "episode_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_entries_last_upgraded_at",
                schema: "events",
                table: "custom_format_score_entries",
                column: "last_upgraded_at");

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_history_arr_instance_id_external_item_i",
                schema: "events",
                table: "custom_format_score_history",
                columns: new[] { "arr_instance_id", "external_item_id", "episode_id" });

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_history_recorded_at",
                schema: "events",
                table: "custom_format_score_history",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_download_items_download_id",
                schema: "events",
                table: "download_items",
                column: "download_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_arr_instance_id",
                schema: "events",
                table: "events",
                column: "arr_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_cycle_id",
                schema: "events",
                table: "events",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_delete_reason",
                schema: "events",
                table: "events",
                column: "delete_reason");

            migrationBuilder.CreateIndex(
                name: "ix_events_event_type",
                schema: "events",
                table: "events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_events_job_run_id",
                schema: "events",
                table: "events",
                column: "job_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_severity",
                schema: "events",
                table: "events",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_events_strike_id",
                schema: "events",
                table: "events",
                column: "strike_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_timestamp",
                schema: "events",
                table: "events",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_started_at",
                schema: "events",
                table: "job_runs",
                column: "started_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_type",
                schema: "events",
                table: "job_runs",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_instance_type",
                schema: "events",
                table: "manual_events",
                column: "instance_type");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_is_resolved",
                schema: "events",
                table: "manual_events",
                column: "is_resolved");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_job_run_id",
                schema: "events",
                table: "manual_events",
                column: "job_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_message",
                schema: "events",
                table: "manual_events",
                column: "message");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_severity",
                schema: "events",
                table: "manual_events",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_timestamp",
                schema: "events",
                table: "manual_events",
                column: "timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_type_item_hash",
                schema: "events",
                table: "manual_events",
                columns: new[] { "type", "item_hash" },
                unique: true,
                filter: "is_resolved = false");

            migrationBuilder.CreateIndex(
                name: "ix_search_queue_arr_instance_id",
                schema: "events",
                table: "search_queue",
                column: "arr_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_command_trackers_arr_instance_id",
                schema: "events",
                table: "seeker_command_trackers",
                column: "arr_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_s",
                schema: "events",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type", "season_number", "cycle_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_strikes_created_at",
                schema: "events",
                table: "strikes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_strikes_download_item_id_type",
                schema: "events",
                table: "strikes",
                columns: new[] { "download_item_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_strikes_job_run_id",
                schema: "events",
                table: "strikes",
                column: "job_run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_format_score_entries",
                schema: "events");

            migrationBuilder.DropTable(
                name: "custom_format_score_history",
                schema: "events");

            migrationBuilder.DropTable(
                name: "events",
                schema: "events");

            migrationBuilder.DropTable(
                name: "manual_events",
                schema: "events");

            migrationBuilder.DropTable(
                name: "search_queue",
                schema: "events");

            migrationBuilder.DropTable(
                name: "seeker_command_trackers",
                schema: "events");

            migrationBuilder.DropTable(
                name: "seeker_history",
                schema: "events");

            migrationBuilder.DropTable(
                name: "strikes",
                schema: "events");

            migrationBuilder.DropTable(
                name: "download_items",
                schema: "events");

            migrationBuilder.DropTable(
                name: "job_runs",
                schema: "events");
        }
    }
}
