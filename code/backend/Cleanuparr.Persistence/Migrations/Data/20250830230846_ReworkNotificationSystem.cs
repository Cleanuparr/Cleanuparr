using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class ReworkNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    on_failed_import_strike = table.Column<bool>(type: "INTEGER", nullable: false),
                    on_stalled_strike = table.Column<bool>(type: "INTEGER", nullable: false),
                    on_slow_strike = table.Column<bool>(type: "INTEGER", nullable: false),
                    on_queue_item_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    on_download_cleaned = table.Column<bool>(type: "INTEGER", nullable: false),
                    on_category_changed = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_configs", x => x.id);
                });
            
            string newGuid = Guid.NewGuid().ToString().ToUpperInvariant();
            migrationBuilder.Sql(
                $"""
                INSERT INTO notification_configs (id, name, type, is_enabled, on_failed_import_strike, on_stalled_strike, on_slow_strike, on_queue_item_deleted, on_download_cleaned, on_category_changed, created_at, updated_at)
                SELECT 
                    '{newGuid}' AS id,
                    'Notifiarr' AS name,
                    'notifiarr' AS type,
                    CASE WHEN (on_failed_import_strike = 1 OR on_stalled_strike = 1 OR on_slow_strike = 1 OR on_queue_item_deleted = 1 OR on_download_cleaned = 1 OR on_category_changed = 1) THEN 1 ELSE 0 END AS is_enabled,
                    on_failed_import_strike,
                    on_stalled_strike,
                    on_slow_strike,
                    on_queue_item_deleted,
                    on_download_cleaned,
                    on_category_changed,
                    datetime('now') AS created_at,
                    datetime('now') AS updated_at
                FROM notifiarr_configs
                WHERE
                    channel_id IS NOT NULL AND channel_id != '' AND api_key IS NOT NULL AND api_key != ''
                """);

            newGuid = Guid.NewGuid().ToString().ToUpperInvariant();
            migrationBuilder.Sql(
                $"""
                INSERT INTO notification_configs (id, name, type, is_enabled, on_failed_import_strike, on_stalled_strike, on_slow_strike, on_queue_item_deleted, on_download_cleaned, on_category_changed, created_at, updated_at)
                SELECT 
                    '{newGuid}' AS id,
                    'Apprise' AS name,
                    'apprise' AS type,
                    CASE WHEN (on_failed_import_strike = 1 OR on_stalled_strike = 1 OR on_slow_strike = 1 OR on_queue_item_deleted = 1 OR on_download_cleaned = 1 OR on_category_changed = 1) THEN 1 ELSE 0 END AS is_enabled,
                    on_failed_import_strike,
                    on_stalled_strike,
                    on_slow_strike,
                    on_queue_item_deleted,
                    on_download_cleaned,
                    on_category_changed,
                    datetime('now') AS created_at,
                    datetime('now') AS updated_at
                FROM apprise_configs
                WHERE
                    key IS NOT NULL AND key != '' AND full_url IS NOT NULL AND full_url != ''
                """);
            
            migrationBuilder.DropColumn(
                name: "on_category_changed",
                table: "notifiarr_configs");

            migrationBuilder.DropColumn(
                name: "on_download_cleaned",
                table: "notifiarr_configs");

            migrationBuilder.DropColumn(
                name: "on_failed_import_strike",
                table: "notifiarr_configs");

            migrationBuilder.DropColumn(
                name: "on_queue_item_deleted",
                table: "notifiarr_configs");

            migrationBuilder.DropColumn(
                name: "on_slow_strike",
                table: "notifiarr_configs");

            migrationBuilder.DropColumn(
                name: "on_stalled_strike",
                table: "notifiarr_configs");

            migrationBuilder.DropColumn(
                name: "on_category_changed",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "on_download_cleaned",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "on_failed_import_strike",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "on_queue_item_deleted",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "on_slow_strike",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "on_stalled_strike",
                table: "apprise_configs");

            migrationBuilder.AlterColumn<string>(
                name: "channel_id",
                table: "notifiarr_configs",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "api_key",
                table: "notifiarr_configs",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "notification_config_id",
                table: "notifiarr_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            
            migrationBuilder.Sql(
                """
                UPDATE notifiarr_configs
                SET notification_config_id = (
                    SELECT id FROM notification_configs
                    WHERE type = 'notifiarr'
                    LIMIT 1
                )
                WHERE channel_id IS NOT NULL AND channel_id != '' AND api_key IS NOT NULL AND api_key != ''
                """);
            
            migrationBuilder.Sql(
                """
                DELETE FROM notifiarr_configs
                WHERE NOT EXISTS (
                    SELECT 1 FROM notification_configs
                    WHERE type = 'notifiarr'
                )
                """);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "apprise_configs",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
            
            migrationBuilder.AddColumn<string>(
                name: "url",
                table: "apprise_configs",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "notification_config_id",
                table: "apprise_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            
            migrationBuilder.Sql(
                """
                UPDATE apprise_configs
                SET notification_config_id = (
                    SELECT id FROM notification_configs
                    WHERE type = 'apprise'
                    LIMIT 1
                ),
                    url = full_url
                WHERE key IS NOT NULL AND key != '' AND full_url IS NOT NULL AND full_url != ''
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM apprise_configs
                WHERE NOT EXISTS (
                    SELECT 1 FROM notification_configs
                    WHERE type = 'apprise'
                )
                """);
            
            migrationBuilder.DropColumn(
                name: "full_url",
                table: "apprise_configs");

            migrationBuilder.CreateIndex(
                name: "ix_notifiarr_configs_notification_config_id",
                table: "notifiarr_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_apprise_configs_notification_config_id",
                table: "apprise_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_configs_name",
                table: "notification_configs",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_apprise_configs_notification_configs_notification_config_id",
                table: "apprise_configs",
                column: "notification_config_id",
                principalTable: "notification_configs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notifiarr_configs_notification_configs_notification_config_id",
                table: "notifiarr_configs",
                column: "notification_config_id",
                principalTable: "notification_configs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_apprise_configs_notification_configs_notification_config_id",
                table: "apprise_configs");

            migrationBuilder.DropForeignKey(
                name: "fk_notifiarr_configs_notification_configs_notification_config_id",
                table: "notifiarr_configs");

            migrationBuilder.DropTable(
                name: "notification_configs");

            migrationBuilder.DropIndex(
                name: "ix_notifiarr_configs_notification_config_id",
                table: "notifiarr_configs");

            migrationBuilder.DropIndex(
                name: "ix_apprise_configs_notification_config_id",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "notification_config_id",
                table: "notifiarr_configs");

            migrationBuilder.DropColumn(
                name: "notification_config_id",
                table: "apprise_configs");

            migrationBuilder.DropColumn(
                name: "url",
                table: "apprise_configs");

            migrationBuilder.AlterColumn<string>(
                name: "channel_id",
                table: "notifiarr_configs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "api_key",
                table: "notifiarr_configs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<bool>(
                name: "on_category_changed",
                table: "notifiarr_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_download_cleaned",
                table: "notifiarr_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_failed_import_strike",
                table: "notifiarr_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_queue_item_deleted",
                table: "notifiarr_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_slow_strike",
                table: "notifiarr_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_stalled_strike",
                table: "notifiarr_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "apprise_configs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "full_url",
                table: "apprise_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "on_category_changed",
                table: "apprise_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_download_cleaned",
                table: "apprise_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_failed_import_strike",
                table: "apprise_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_queue_item_deleted",
                table: "apprise_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_slow_strike",
                table: "apprise_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "on_stalled_strike",
                table: "apprise_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
