using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class UpdateSearchDelay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE general_configs
                SET search_delay = 60
                WHERE search_delay < 30;
            """);
            
            migrationBuilder.Sql("""
                UPDATE general_configs
                SET search_delay = 120
                WHERE search_delay >= 30 AND search_delay < 120;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
