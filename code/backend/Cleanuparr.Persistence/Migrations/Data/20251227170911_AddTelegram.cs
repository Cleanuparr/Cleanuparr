using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddTelegram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bot_token = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    chat_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    topic_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    send_silently = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telegram_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_telegram_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_telegram_configs_notification_config_id",
                table: "telegram_configs",
                column: "notification_config_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_configs");
        }
    }
}
