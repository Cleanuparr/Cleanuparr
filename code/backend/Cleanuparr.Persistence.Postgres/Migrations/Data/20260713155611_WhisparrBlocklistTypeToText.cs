using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class WhisparrBlocklistTypeToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE data.content_blocker_configs ALTER COLUMN whisparr_blocklist_type TYPE text USING CASE whisparr_blocklist_type WHEN 0 THEN 'blacklist' WHEN 1 THEN 'whitelist' END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE data.content_blocker_configs ALTER COLUMN whisparr_blocklist_type TYPE integer USING CASE whisparr_blocklist_type WHEN 'blacklist' THEN 0 WHEN 'whitelist' THEN 1 END;");
        }
    }
}
