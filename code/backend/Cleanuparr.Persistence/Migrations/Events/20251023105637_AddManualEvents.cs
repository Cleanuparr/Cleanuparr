using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddManualEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manual_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: true),
                    severity = table.Column<string>(type: "TEXT", nullable: false),
                    is_resolved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manual_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_is_resolved",
                table: "manual_events",
                column: "is_resolved");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_message",
                table: "manual_events",
                column: "message");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_severity",
                table: "manual_events",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_timestamp",
                table: "manual_events",
                column: "timestamp",
                descending: [true]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manual_events");
        }
    }
}
