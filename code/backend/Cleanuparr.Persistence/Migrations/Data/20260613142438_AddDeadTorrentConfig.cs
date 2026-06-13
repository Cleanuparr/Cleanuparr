using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddDeadTorrentConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dead_torrent_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    target_category = table.Column<string>(type: "TEXT", nullable: false),
                    use_tag = table.Column<bool>(type: "INTEGER", nullable: false),
                    max_strikes = table.Column<int>(type: "INTEGER", nullable: false),
                    categories = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dead_torrent_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_dead_torrent_configs_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dead_torrent_configs_download_client_config_id",
                table: "dead_torrent_configs",
                column: "download_client_config_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_torrent_configs");
        }
    }
}
