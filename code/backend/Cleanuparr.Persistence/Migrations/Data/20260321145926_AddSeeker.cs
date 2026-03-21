using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeeker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "on_search_triggered",
                table: "notification_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "custom_format_score_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    episode_id = table.Column<long>(type: "INTEGER", nullable: false),
                    item_type = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    file_id = table.Column<long>(type: "INTEGER", nullable: false),
                    current_score = table.Column<int>(type: "INTEGER", nullable: false),
                    cutoff_score = table.Column<int>(type: "INTEGER", nullable: false),
                    quality_profile_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_format_score_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_custom_format_score_entries_arr_instances_arr_instance_id",
                        column: x => x.arr_instance_id,
                        principalTable: "arr_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "custom_format_score_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    episode_id = table.Column<long>(type: "INTEGER", nullable: false),
                    item_type = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    score = table.Column<int>(type: "INTEGER", nullable: false),
                    cutoff_score = table.Column<int>(type: "INTEGER", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_format_score_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_custom_format_score_history_arr_instances_arr_instance_id",
                        column: x => x.arr_instance_id,
                        principalTable: "arr_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "seeker_command_trackers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    command_id = table.Column<long>(type: "INTEGER", nullable: false),
                    event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    item_title = table.Column<string>(type: "TEXT", nullable: false),
                    item_type = table.Column<string>(type: "TEXT", nullable: false),
                    season_number = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_command_trackers", x => x.id);
                    table.ForeignKey(
                        name: "fk_seeker_command_trackers_arr_instances_arr_instance_id",
                        column: x => x.arr_instance_id,
                        principalTable: "arr_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seeker_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    search_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    search_interval = table.Column<ushort>(type: "INTEGER", nullable: false),
                    proactive_search_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    selection_strategy = table.Column<string>(type: "TEXT", nullable: false),
                    monitored_only = table.Column<bool>(type: "INTEGER", nullable: false),
                    use_cutoff = table.Column<bool>(type: "INTEGER", nullable: false),
                    use_custom_format_score = table.Column<bool>(type: "INTEGER", nullable: false),
                    use_round_robin = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_configs", x => x.id);
                });
            
            // Migrate old data
            migrationBuilder.InsertData(
                table: "seeker_configs",
                columns: new[] { "id", "search_enabled", "search_interval", "proactive_search_enabled", "selection_strategy", "monitored_only", "use_cutoff", "use_custom_format_score", "use_round_robin" },
                values: new object[] { Guid.NewGuid(), true, 10, false, "balancedweighted", true, true, true, true });
    
            migrationBuilder.Sql(@"
                UPDATE seeker_configs SET search_enabled = (
                    SELECT COALESCE(g.search_enabled, 1) FROM general_configs g LIMIT 1
                )");

            migrationBuilder.CreateTable(
                name: "seeker_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    item_type = table.Column<string>(type: "TEXT", nullable: false),
                    season_number = table.Column<int>(type: "INTEGER", nullable: false),
                    run_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_searched_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    item_title = table.Column<string>(type: "TEXT", nullable: false),
                    search_count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_seeker_history_arr_instances_arr_instance_id",
                        column: x => x.arr_instance_id,
                        principalTable: "arr_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seeker_instance_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    skip_tags = table.Column<string>(type: "TEXT", nullable: false),
                    last_processed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    current_run_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    total_eligible_items = table.Column<int>(type: "INTEGER", nullable: false),
                    active_download_limit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_instance_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_seeker_instance_configs_arr_instances_arr_instance_id",
                        column: x => x.arr_instance_id,
                        principalTable: "arr_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
            
            migrationBuilder.DropColumn(
                name: "search_delay",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "search_enabled",
                table: "general_configs");

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_entries_arr_instance_id_external_item_id_episode_id",
                table: "custom_format_score_entries",
                columns: new[] { "arr_instance_id", "external_item_id", "episode_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_history_arr_instance_id_external_item_id_episode_id",
                table: "custom_format_score_history",
                columns: new[] { "arr_instance_id", "external_item_id", "episode_id" });

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_history_recorded_at",
                table: "custom_format_score_history",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_search_queue_arr_instance_id",
                table: "search_queue",
                column: "arr_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_command_trackers_arr_instance_id",
                table: "seeker_command_trackers",
                column: "arr_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_season_number_run_id",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type", "season_number", "run_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seeker_instance_configs_arr_instance_id",
                table: "seeker_instance_configs",
                column: "arr_instance_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_format_score_entries");

            migrationBuilder.DropTable(
                name: "custom_format_score_history");

            migrationBuilder.DropTable(
                name: "search_queue");

            migrationBuilder.DropTable(
                name: "seeker_command_trackers");

            migrationBuilder.DropTable(
                name: "seeker_configs");

            migrationBuilder.DropTable(
                name: "seeker_history");

            migrationBuilder.DropTable(
                name: "seeker_instance_configs");

            migrationBuilder.DropColumn(
                name: "on_search_triggered",
                table: "notification_configs");

            migrationBuilder.AddColumn<ushort>(
                name: "search_delay",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<bool>(
                name: "search_enabled",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
