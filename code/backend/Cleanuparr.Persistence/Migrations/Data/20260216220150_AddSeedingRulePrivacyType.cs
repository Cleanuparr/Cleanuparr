using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddSeedingRulePrivacyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "privacy_type",
                table: "seeding_rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "both");

            // Migrate existing data: if DeletePrivate was true, rules apply to "both";
            // if false, rules only apply to "public" (preserving existing behavior)
            migrationBuilder.Sql("""
                UPDATE seeding_rules
                SET privacy_type = CASE
                    WHEN (SELECT delete_private FROM download_cleaner_configs LIMIT 1) = 1 THEN 'both'
                    ELSE 'public'
                END
            """);

            migrationBuilder.DropColumn(
                name: "delete_private",
                table: "download_cleaner_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "privacy_type",
                table: "seeding_rules");

            migrationBuilder.AddColumn<bool>(
                name: "delete_private",
                table: "download_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
