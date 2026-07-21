#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class InitialEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    event_type = table.Column<string>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: true),
                    severity = table.Column<string>(type: "TEXT", nullable: false),
                    tracking_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_events_event_type",
                table: "events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_events_message",
                table: "events",
                column: "message");

            migrationBuilder.CreateIndex(
                name: "ix_events_severity",
                table: "events",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_events_timestamp",
                table: "events",
                column: "timestamp",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "events");
        }
    }
}
