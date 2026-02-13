using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddOnStrikeDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_strikes_strike_id",
                table: "events");

            migrationBuilder.AddForeignKey(
                name: "fk_events_strikes_strike_id",
                table: "events",
                column: "strike_id",
                principalTable: "strikes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_strikes_strike_id",
                table: "events");

            migrationBuilder.AddForeignKey(
                name: "fk_events_strikes_strike_id",
                table: "events",
                column: "strike_id",
                principalTable: "strikes",
                principalColumn: "id");
        }
    }
}
