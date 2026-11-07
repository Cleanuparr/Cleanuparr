using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeekerRadarrReleaseGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "radarr_release_gate",
                table: "seeker_instance_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "releasedate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "radarr_release_gate",
                table: "seeker_instance_configs");
        }
    }
}
