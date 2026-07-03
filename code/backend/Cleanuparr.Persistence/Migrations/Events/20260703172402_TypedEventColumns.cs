using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class TypedEventColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fresh start
            migrationBuilder.Sql("DELETE FROM events;");
            migrationBuilder.Sql("DELETE FROM manual_events;");

            migrationBuilder.DropTable(
                name: "search_event_data");

            migrationBuilder.DropColumn(
                name: "data",
                table: "manual_events");

            migrationBuilder.RenameColumn(
                name: "data",
                table: "events",
                newName: "search_type");

            migrationBuilder.AddColumn<string>(
                name: "item_hash",
                table: "manual_events",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "item_title",
                table: "manual_events",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "strike_count",
                table: "manual_events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clean_reason",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cleaned_category",
                table: "events",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delete_reason",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failed_import_reasons",
                table: "events",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "grabbed_items",
                table: "events",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "is_category_tag",
                table: "events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "item_hash",
                table: "events",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "item_title",
                table: "events",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "new_category",
                table: "events",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_category",
                table: "events",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "remove_from_client",
                table: "events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_reason",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "seed_ratio",
                table: "events",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "seeding_time_hours",
                table: "events",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "strike_count",
                table: "events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_clean_reason",
                table: "events",
                column: "clean_reason");

            migrationBuilder.CreateIndex(
                name: "ix_events_delete_reason",
                table: "events",
                column: "delete_reason");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_events_clean_reason",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_delete_reason",
                table: "events");

            migrationBuilder.DropColumn(
                name: "item_hash",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "item_title",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "strike_count",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "clean_reason",
                table: "events");

            migrationBuilder.DropColumn(
                name: "cleaned_category",
                table: "events");

            migrationBuilder.DropColumn(
                name: "delete_reason",
                table: "events");

            migrationBuilder.DropColumn(
                name: "failed_import_reasons",
                table: "events");

            migrationBuilder.DropColumn(
                name: "grabbed_items",
                table: "events");

            migrationBuilder.DropColumn(
                name: "is_category_tag",
                table: "events");

            migrationBuilder.DropColumn(
                name: "item_hash",
                table: "events");

            migrationBuilder.DropColumn(
                name: "item_title",
                table: "events");

            migrationBuilder.DropColumn(
                name: "new_category",
                table: "events");

            migrationBuilder.DropColumn(
                name: "old_category",
                table: "events");

            migrationBuilder.DropColumn(
                name: "remove_from_client",
                table: "events");

            migrationBuilder.DropColumn(
                name: "search_reason",
                table: "events");

            migrationBuilder.DropColumn(
                name: "seed_ratio",
                table: "events");

            migrationBuilder.DropColumn(
                name: "seeding_time_hours",
                table: "events");

            migrationBuilder.DropColumn(
                name: "strike_count",
                table: "events");

            migrationBuilder.RenameColumn(
                name: "search_type",
                table: "events",
                newName: "data");

            migrationBuilder.AddColumn<string>(
                name: "data",
                table: "manual_events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "search_event_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    app_event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    grabbed_items = table.Column<string>(type: "TEXT", nullable: false),
                    item_title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    search_reason = table.Column<string>(type: "TEXT", nullable: false),
                    search_type = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "ix_search_event_data_app_event_id",
                table: "search_event_data",
                column: "app_event_id",
                unique: true);
        }
    }
}
