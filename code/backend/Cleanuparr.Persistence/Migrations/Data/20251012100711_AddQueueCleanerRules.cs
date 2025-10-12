using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddQueueCleanerRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "stalled_downloading_metadata_max_strikes",
                table: "queue_cleaner_configs",
                newName: "downloading_metadata_max_strikes");

            migrationBuilder.CreateTable(
                name: "slow_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reset_strikes_on_progress = table.Column<bool>(type: "INTEGER", nullable: false),
                    max_time_hours = table.Column<double>(type: "REAL", nullable: false),
                    min_speed = table.Column<string>(type: "TEXT", nullable: false),
                    ignore_above_size = table.Column<string>(type: "TEXT", nullable: true),
                    queue_cleaner_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    max_strikes = table.Column<int>(type: "INTEGER", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false),
                    min_completion_percentage = table.Column<ushort>(type: "INTEGER", nullable: false),
                    max_completion_percentage = table.Column<ushort>(type: "INTEGER", nullable: false),
                    delete_private_torrents_from_client = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slow_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_slow_rules_queue_cleaner_configs_queue_cleaner_config_id",
                        column: x => x.queue_cleaner_config_id,
                        principalTable: "queue_cleaner_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stall_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reset_strikes_on_progress = table.Column<bool>(type: "INTEGER", nullable: false),
                    minimum_progress = table.Column<string>(type: "TEXT", nullable: true),
                    queue_cleaner_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    max_strikes = table.Column<int>(type: "INTEGER", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false),
                    min_completion_percentage = table.Column<ushort>(type: "INTEGER", nullable: false),
                    max_completion_percentage = table.Column<ushort>(type: "INTEGER", nullable: false),
                    delete_private_torrents_from_client = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stall_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_stall_rules_queue_cleaner_configs_queue_cleaner_config_id",
                        column: x => x.queue_cleaner_config_id,
                        principalTable: "queue_cleaner_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_slow_rules_queue_cleaner_config_id",
                table: "slow_rules",
                column: "queue_cleaner_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_stall_rules_queue_cleaner_config_id",
                table: "stall_rules",
                column: "queue_cleaner_config_id");
            
            string newGuid = Guid.NewGuid().ToString().ToUpperInvariant();
            migrationBuilder.Sql($"""
                INSERT INTO slow_rules (
                    id,
                    reset_strikes_on_progress,
                    max_time_hours,
                    min_speed,
                    ignore_above_size,
                    queue_cleaner_config_id,
                    name,
                    enabled,
                    max_strikes,
                    privacy_type,
                    min_completion_percentage,
                    max_completion_percentage,
                    delete_private_torrents_from_client
                )
                SELECT
                    '{newGuid}',
                    slow_reset_strikes_on_progress,
                    slow_max_time,
                    COALESCE(slow_min_speed, ''),
                    NULLIF(slow_ignore_above_size, ''),
                    id,
                    'Legacy Slow Rule',
                    1,
                    slow_max_strikes,
                    CASE WHEN slow_ignore_private = 1 THEN 'public' ELSE 'both' END,
                    0,
                    100,
                    slow_delete_private
                FROM queue_cleaner_configs
                WHERE slow_max_strikes >= 3;
            """);
            
            newGuid = Guid.NewGuid().ToString().ToUpperInvariant();
            migrationBuilder.Sql($"""
                INSERT INTO stall_rules (
                    id,
                    reset_strikes_on_progress,
                    minimum_progress,
                    queue_cleaner_config_id,
                    name,
                    enabled,
                    max_strikes,
                    privacy_type,
                    min_completion_percentage,
                    max_completion_percentage,
                    delete_private_torrents_from_client
                )
                SELECT
                    '{newGuid}',
                    stalled_reset_strikes_on_progress,
                    NULL,
                    id,
                    'Legacy Stall Rule',
                    1,
                    stalled_max_strikes,
                    CASE WHEN stalled_ignore_private = 1 THEN 'public' ELSE 'both' END,
                    0,
                    100,
                    stalled_delete_private
                FROM queue_cleaner_configs
                WHERE stalled_max_strikes >= 3;
            """);

            migrationBuilder.DropColumn(
                name: "slow_delete_private",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "slow_ignore_above_size",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "slow_ignore_private",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "slow_max_strikes",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "slow_max_time",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "slow_min_speed",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "slow_reset_strikes_on_progress",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "stalled_delete_private",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "stalled_ignore_private",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "stalled_max_strikes",
                table: "queue_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "stalled_reset_strikes_on_progress",
                table: "queue_cleaner_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "slow_rules");

            migrationBuilder.DropTable(
                name: "stall_rules");

            migrationBuilder.RenameColumn(
                name: "downloading_metadata_max_strikes",
                table: "queue_cleaner_configs",
                newName: "stalled_downloading_metadata_max_strikes");

            migrationBuilder.AddColumn<bool>(
                name: "slow_delete_private",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "slow_ignore_above_size",
                table: "queue_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "slow_ignore_private",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<ushort>(
                name: "slow_max_strikes",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<double>(
                name: "slow_max_time",
                table: "queue_cleaner_configs",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "slow_min_speed",
                table: "queue_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "slow_reset_strikes_on_progress",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "stalled_delete_private",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "stalled_ignore_private",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<ushort>(
                name: "stalled_max_strikes",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<bool>(
                name: "stalled_reset_strikes_on_progress",
                table: "queue_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
