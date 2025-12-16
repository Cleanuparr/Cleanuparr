using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class RenameCleanCategoryToSeedingRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seeding_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_cleaner_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_seeding_rules_download_cleaner_configs_download_cleaner_config_id",
                        column: x => x.download_cleaner_config_id,
                        principalTable: "download_cleaner_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_seeding_rules_download_cleaner_config_id",
                table: "seeding_rules",
                column: "download_cleaner_config_id");

            migrationBuilder.Sql(@"
                INSERT INTO seeding_rules (id, download_cleaner_config_id, name, max_ratio, min_seed_time, max_seed_time, delete_source_files)
                SELECT id, download_cleaner_config_id, name, max_ratio, min_seed_time, max_seed_time, delete_source_files
                FROM clean_categories;
            ");

            migrationBuilder.DropTable(
                name: "clean_categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clean_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_cleaner_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clean_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_clean_categories_download_cleaner_configs_download_cleaner_config_id",
                        column: x => x.download_cleaner_config_id,
                        principalTable: "download_cleaner_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clean_categories_download_cleaner_config_id",
                table: "clean_categories",
                column: "download_cleaner_config_id");

            migrationBuilder.Sql(@"
                INSERT INTO clean_categories (id, download_cleaner_config_id, name, max_ratio, min_seed_time, max_seed_time, delete_source_files)
                SELECT id, download_cleaner_config_id, name, max_ratio, min_seed_time, max_seed_time, delete_source_files
                FROM seeding_rules;
            ");

            migrationBuilder.DropTable(
                name: "seeding_rules");
        }
    }
}
