using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cleanuparr.Shared.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class MoveSeekerStateToEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            string configPath = ConfigurationPathProvider.GetConfigPath();
            string eventsDbPath = Path.Combine(configPath, "events.db");
            string dataDbPath = Path.Combine(configPath, "cleanuparr.db");

            if (!TableExists(eventsDbPath, "custom_format_score_entries"))
            {
                CreateTables(migrationBuilder);
            }

            CopyFromDataDatabase(migrationBuilder, dataDbPath);
        }

        private static void CreateTables(MigrationBuilder migrationBuilder)
        {
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
                    is_monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_synced_at = table.Column<string>(type: "TEXT", nullable: false),
                    last_upgraded_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_format_score_entries", x => x.id);
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
                    recorded_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_format_score_history", x => x.id);
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
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_queue", x => x.id);
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
                    season_number = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_command_trackers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seeker_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    item_type = table.Column<string>(type: "TEXT", nullable: false),
                    season_number = table.Column<int>(type: "INTEGER", nullable: false),
                    cycle_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_searched_at = table.Column<string>(type: "TEXT", nullable: false),
                    item_title = table.Column<string>(type: "TEXT", nullable: false),
                    search_count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_dry_run = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_entries_arr_instance_id_external_item_id_episode_id",
                table: "custom_format_score_entries",
                columns: new[] { "arr_instance_id", "external_item_id", "episode_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_custom_format_score_entries_last_upgraded_at",
                table: "custom_format_score_entries",
                column: "last_upgraded_at");

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
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type_season_number_cycle_id",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type", "season_number", "cycle_id" },
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
                name: "seeker_history");
        }

        private static bool TableExists(string dbPath, string tableName)
        {
            if (!File.Exists(dbPath))
            {
                return false;
            }

            using SqliteConnection connection = new($"Data Source={dbPath}");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
            command.Parameters.AddWithValue("$name", tableName);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        private static void CopyFromDataDatabase(MigrationBuilder migrationBuilder, string dataDbPath)
        {
            string[] tables =
            {
                "custom_format_score_entries",
                "custom_format_score_history",
                "search_queue",
                "seeker_command_trackers",
                "seeker_history",
            };

            List<string> present = tables.Where(t => TableExists(dataDbPath, t)).ToList();
            if (present.Count == 0)
            {
                return;
            }

            StringBuilder sql = new();
            sql.AppendLine($"ATTACH DATABASE '{dataDbPath.Replace("'", "''")}' AS src;");
            foreach (string table in present)
            {
                sql.AppendLine($"INSERT INTO {table} SELECT * FROM src.{table} WHERE NOT EXISTS (SELECT 1 FROM {table});");
            }

            foreach (string table in present)
            {
                sql.AppendLine($"DROP TABLE IF EXISTS src.{table};");
            }

            sql.AppendLine("DETACH DATABASE src;");

            migrationBuilder.Sql(sql.ToString(), suppressTransaction: true);
        }
    }
}
