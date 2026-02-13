using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddStrikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "download_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_download_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "strikes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_item_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_downloaded_bytes = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strikes", x => x.id);
                    table.ForeignKey(
                        name: "fk_strikes_download_items_download_item_id",
                        column: x => x.download_item_id,
                        principalTable: "download_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_download_items_download_id",
                table: "download_items",
                column: "download_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_strikes_created_at",
                table: "strikes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_strikes_download_item_id_type",
                table: "strikes",
                columns: new[] { "download_item_id", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "strikes");

            migrationBuilder.DropTable(
                name: "download_items");
        }
    }
}
