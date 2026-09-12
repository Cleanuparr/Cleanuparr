using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class RemoveEncryptionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encryption_key",
                schema: "data",
                table: "general_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encryption_key",
                schema: "data",
                table: "general_configs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
