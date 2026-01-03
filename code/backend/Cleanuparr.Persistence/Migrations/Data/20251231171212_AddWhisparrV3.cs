using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddWhisparrV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "version",
                table: "arr_instances",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.Sql(
                """
                UPDATE arr_instances
                SET version = CASE
                    WHEN (
                        SELECT type
                        FROM arr_configs
                        WHERE arr_configs.id = arr_instances.arr_config_id
                    ) = 'sonarr' THEN 4
                    WHEN (
                        SELECT type
                        FROM arr_configs
                        WHERE arr_configs.id = arr_instances.arr_config_id
                    ) = 'radarr' THEN 6
                    WHEN (
                        SELECT type
                        FROM arr_configs
                        WHERE arr_configs.id = arr_instances.arr_config_id
                    ) = 'lidarr' THEN 3
                    WHEN (
                        SELECT type
                        FROM arr_configs
                        WHERE arr_configs.id = arr_instances.arr_config_id
                    ) = 'readarr' THEN 0.4
                    WHEN (
                        SELECT type
                        FROM arr_configs
                        WHERE arr_configs.id = arr_instances.arr_config_id
                    ) = 'whisparr' THEN 2
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                table: "arr_instances");
        }
    }
}
