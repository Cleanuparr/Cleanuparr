using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddBlacklistSyncSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "blacklist_path",
                table: "general_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "enable_blacklist_sync",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "blacklist_sync_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    hash = table.Column<string>(type: "TEXT", nullable: false),
                    download_client_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blacklist_sync_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_blacklist_sync_history_download_clients_download_client_id",
                        column: x => x.download_client_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_sync_history_download_client_id",
                table: "blacklist_sync_history",
                column: "download_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_sync_history_hash",
                table: "blacklist_sync_history",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_sync_history_hash_download_client_id",
                table: "blacklist_sync_history",
                columns: new[] { "hash", "download_client_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blacklist_sync_history");

            migrationBuilder.DropColumn(
                name: "blacklist_path",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "enable_blacklist_sync",
                table: "general_configs");
        }
    }
}
