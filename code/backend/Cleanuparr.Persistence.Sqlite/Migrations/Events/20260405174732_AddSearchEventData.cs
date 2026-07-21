using System;
using Cleanuparr.Shared.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddSearchEventData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Introspect current schema to make migration idempotent.
            // The ATTACH DATABASE below requires suppressTransaction: true, which causes
            // EF Core to commit preceding schema changes before executing it. If the ATTACH
            // or any later step fails, those schema changes are committed but the migration
            // is not recorded — leading to "duplicate column name" on retry.
            string eventsDbPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "events.db");

            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasSearchEventDataTable = false;

            if (File.Exists(eventsDbPath))
            {
                using var connection = new SqliteConnection($"Data Source={eventsDbPath}");
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM pragma_table_info('events')";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        existingColumns.Add(reader.GetString(0));
                    }
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='search_event_data'";
                    hasSearchEventDataTable = Convert.ToInt64(cmd.ExecuteScalar()) > 0;
                }
            }

            if (!existingColumns.Contains("arr_instance_id"))
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "arr_instance_id",
                    table: "events",
                    type: "TEXT",
                    nullable: true);
            }

            if (!existingColumns.Contains("download_client_id"))
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "download_client_id",
                    table: "events",
                    type: "TEXT",
                    nullable: true);
            }

            if (!hasSearchEventDataTable)
            {
                migrationBuilder.CreateTable(
                    name: "search_event_data",
                    columns: table => new
                    {
                        id = table.Column<Guid>(type: "TEXT", nullable: false),
                        app_event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                        item_title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                        search_type = table.Column<string>(type: "TEXT", nullable: false),
                        search_reason = table.Column<string>(type: "TEXT", nullable: false),
                        grabbed_items = table.Column<string>(type: "TEXT", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("pk_search_event_data", x => x.id);
                        table.ForeignKey(
                            name: "fk_search_event_data_events_app_event_id",
                            column: x => x.app_event_id,
                            principalTable: "events",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade);
                    });
            }

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_events_arr_instance_id ON events (arr_instance_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_events_download_client_id ON events (download_client_id);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_search_event_data_app_event_id ON search_event_data (app_event_id);");

            string dataDbPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "cleanuparr.db");

            if (File.Exists(dataDbPath))
            {
                migrationBuilder.Sql($"""
                    ATTACH DATABASE '{dataDbPath}' AS main_db;

                    UPDATE events
                    SET arr_instance_id = (
                        SELECT a.id FROM main_db.arr_instances a
                        WHERE RTRIM(a.url, '/') = RTRIM(events.instance_url, '/')
                           OR RTRIM(a.external_url, '/') = RTRIM(events.instance_url, '/')
                        LIMIT 1
                    )
                    WHERE instance_url IS NOT NULL AND arr_instance_id IS NULL;

                    UPDATE events
                    SET download_client_id = (
                        SELECT dc.id FROM main_db.download_clients dc
                        WHERE dc.name = events.download_client_name
                        LIMIT 1
                    )
                    WHERE download_client_name IS NOT NULL AND download_client_id IS NULL;

                    DETACH DATABASE main_db;
                    """, suppressTransaction: true);
            }

            migrationBuilder.Sql("""
                INSERT INTO search_event_data (id, app_event_id, item_title, search_type, search_reason, grabbed_items)
                SELECT
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))),
                    e.id,
                    COALESCE(json_extract(e.data, '$.Items[0]'), 'Unknown'),
                    COALESCE(LOWER(json_extract(e.data, '$.SearchType')), 'proactive'),
                    'missing',
                    COALESCE(
                        (SELECT json_group_array(json_extract(value, '$.Title'))
                         FROM json_each(json_extract(e.data, '$.GrabbedItems'))),
                        '[]'
                    )
                FROM events e
                WHERE e.event_type = 'searchtriggered'
                  AND e.data IS NOT NULL
                  AND e.data != ''
                  AND NOT EXISTS (SELECT 1 FROM search_event_data sed WHERE sed.app_event_id = e.id);
                """);

            migrationBuilder.Sql("""
                UPDATE events
                SET data = NULL
                WHERE event_type = 'searchtriggered';
                """);

            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_events_instance_type;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_events_download_client_type;");

            if (existingColumns.Contains("instance_type"))
            {
                migrationBuilder.DropColumn(
                    name: "instance_type",
                    table: "events");
            }

            if (existingColumns.Contains("instance_url"))
            {
                migrationBuilder.DropColumn(
                    name: "instance_url",
                    table: "events");
            }

            if (existingColumns.Contains("download_client_type"))
            {
                migrationBuilder.DropColumn(
                    name: "download_client_type",
                    table: "events");
            }

            if (existingColumns.Contains("download_client_name"))
            {
                migrationBuilder.DropColumn(
                    name: "download_client_name",
                    table: "events");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_event_data");

            migrationBuilder.DropIndex(
                name: "ix_events_arr_instance_id",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_download_client_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "arr_instance_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "download_client_id",
                table: "events");

            migrationBuilder.AddColumn<string>(
                name: "instance_type",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instance_url",
                table: "events",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_client_type",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_client_name",
                table: "events",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_instance_type",
                table: "events",
                column: "instance_type");

            migrationBuilder.CreateIndex(
                name: "ix_events_download_client_type",
                table: "events",
                column: "download_client_type");
        }
    }
}
