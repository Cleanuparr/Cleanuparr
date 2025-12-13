using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddPushoverProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pushover_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    api_token = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    user_key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    devices = table.Column<string>(type: "TEXT", nullable: false),
                    priority = table.Column<string>(type: "TEXT", nullable: false),
                    sound = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    retry = table.Column<int>(type: "INTEGER", nullable: true),
                    expire = table.Column<int>(type: "INTEGER", nullable: true),
                    tags = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pushover_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_pushover_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pushover_configs_notification_config_id",
                table: "pushover_configs",
                column: "notification_config_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pushover_configs");
        }
    }
}
