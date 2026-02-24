using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddOidcConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "auth_oidc_authorized_subject",
                table: "general_configs",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "auth_oidc_client_id",
                table: "general_configs",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "auth_oidc_client_secret",
                table: "general_configs",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "auth_oidc_enabled",
                table: "general_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "auth_oidc_issuer_url",
                table: "general_configs",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "auth_oidc_provider_name",
                table: "general_configs",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "auth_oidc_scopes",
                table: "general_configs",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auth_oidc_authorized_subject",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_oidc_client_id",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_oidc_client_secret",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_oidc_enabled",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_oidc_issuer_url",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_oidc_provider_name",
                table: "general_configs");

            migrationBuilder.DropColumn(
                name: "auth_oidc_scopes",
                table: "general_configs");
        }
    }
}
