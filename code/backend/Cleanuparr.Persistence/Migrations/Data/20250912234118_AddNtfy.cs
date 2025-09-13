using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddNtfy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ntfy_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    topics = table.Column<string>(type: "TEXT", nullable: false),
                    authentication_type = table.Column<string>(type: "TEXT", nullable: false),
                    username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    password = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    access_token = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    priority = table.Column<string>(type: "TEXT", nullable: false),
                    tags = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ntfy_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_ntfy_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ntfy_configs_notification_config_id",
                table: "ntfy_configs",
                column: "notification_config_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ntfy_configs");
        }
    }
}
