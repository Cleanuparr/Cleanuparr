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
                    use_round_robin = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_configs", x => x.id);
                });
            
            migrationBuilder.InsertData(
                table: "seeker_configs",
                columns: new[] { "id", "search_enabled", "search_interval", "proactive_search_enabled", "selection_strategy", "monitored_only", "use_cutoff", "use_round_robin" },
                values: new object[] { Guid.NewGuid(), true, 3, false, "balancedweighted", true, false, true });
            
            // Migrate old data
            migrationBuilder.Sql(@"
                UPDATE seeker_configs SET search_enabled = (
                    SELECT COALESCE(g.search_enabled, 1) FROM general_configs g LIMIT 1
                )");
            
            migrationBuilder.DropColumn(
                name: "search_delay",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "search_enabled",
                table: "general_configs");

            migrationBuilder.CreateTable(
                name: "seeker_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    item_type = table.Column<string>(type: "TEXT", nullable: false),
                    last_searched_at = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    last_processed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "ix_search_queue_arr_instance_id",
                table: "search_queue",
                column: "arr_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_history_arr_instance_id_external_item_id_item_type",
                table: "seeker_history",
                columns: new[] { "arr_instance_id", "external_item_id", "item_type" },
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
                name: "search_queue");

            migrationBuilder.DropTable(
                name: "seeker_configs");

            migrationBuilder.DropTable(
                name: "seeker_history");

            migrationBuilder.DropTable(
                name: "seeker_instance_configs");

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
