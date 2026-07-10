using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddDeleteSourceFilesToCleanCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "delete_source_files",
                table: "clean_categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
            
            migrationBuilder.Sql("UPDATE clean_categories SET delete_source_files = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delete_source_files",
                table: "clean_categories");
        }
    }
}
