using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddGotify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gotify_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    application_token = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gotify_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_gotify_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gotify_configs_notification_config_id",
                table: "gotify_configs",
                column: "notification_config_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gotify_configs");
        }
    }
}
