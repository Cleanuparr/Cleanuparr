using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Users
{
    /// <inheritdoc />
    public partial class AddOidcConfigToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "oidc_authorized_subject",
                table: "users",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "oidc_client_id",
                table: "users",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "oidc_client_secret",
                table: "users",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "oidc_enabled",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "oidc_issuer_url",
                table: "users",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "oidc_provider_name",
                table: "users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "oidc_scopes",
                table: "users",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "oidc_authorized_subject",
                table: "users");

            migrationBuilder.DropColumn(
                name: "oidc_client_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "oidc_client_secret",
                table: "users");

            migrationBuilder.DropColumn(
                name: "oidc_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "oidc_issuer_url",
                table: "users");

            migrationBuilder.DropColumn(
                name: "oidc_provider_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "oidc_scopes",
                table: "users");
        }
    }
}
