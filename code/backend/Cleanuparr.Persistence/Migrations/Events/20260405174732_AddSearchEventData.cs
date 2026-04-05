using System;
using Cleanuparr.Shared.Helpers;
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
            migrationBuilder.AddColumn<Guid>(
                name: "arr_instance_id",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "download_client_id",
                table: "events",
                type: "TEXT",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_events_arr_instance_id",
                table: "events",
                column: "arr_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_download_client_id",
                table: "events",
                column: "download_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_event_data_app_event_id",
                table: "search_event_data",
                column: "app_event_id",
                unique: true);

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
                  AND e.data != '';
                """);

            migrationBuilder.Sql("""
                UPDATE events
                SET data = NULL
                WHERE event_type = 'searchtriggered';
                """);

            migrationBuilder.DropIndex(
                name: "ix_events_instance_type",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_download_client_type",
                table: "events");

            migrationBuilder.DropColumn(
                name: "instance_type",
                table: "events");

            migrationBuilder.DropColumn(
                name: "instance_url",
                table: "events");

            migrationBuilder.DropColumn(
                name: "download_client_type",
                table: "events");

            migrationBuilder.DropColumn(
                name: "download_client_name",
                table: "events");
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
