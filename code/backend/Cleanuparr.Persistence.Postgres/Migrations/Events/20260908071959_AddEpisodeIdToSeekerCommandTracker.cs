using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddEpisodeIdToSeekerCommandTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "episode_id",
                schema: "events",
                table: "seeker_command_trackers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "episode_id",
                schema: "events",
                table: "seeker_command_trackers");
        }
    }
}
