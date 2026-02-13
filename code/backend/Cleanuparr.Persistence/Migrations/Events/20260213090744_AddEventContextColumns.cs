using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Events
{
    /// <inheritdoc />
    public partial class AddEventContextColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "job_run_id",
                table: "strikes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "download_client_name",
                table: "manual_events",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_client_type",
                table: "manual_events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instance_type",
                table: "manual_events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instance_url",
                table: "manual_events",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "job_run_id",
                table: "manual_events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_client_name",
                table: "events",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_client_type",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instance_type",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instance_url",
                table: "events",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "job_run_id",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "strike_id",
                table: "events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "job_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_strikes_job_run_id",
                table: "strikes",
                column: "job_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_instance_type",
                table: "manual_events",
                column: "instance_type");

            migrationBuilder.CreateIndex(
                name: "ix_manual_events_job_run_id",
                table: "manual_events",
                column: "job_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_download_client_type",
                table: "events",
                column: "download_client_type");

            migrationBuilder.CreateIndex(
                name: "ix_events_instance_type",
                table: "events",
                column: "instance_type");

            migrationBuilder.CreateIndex(
                name: "ix_events_job_run_id",
                table: "events",
                column: "job_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_strike_id",
                table: "events",
                column: "strike_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_started_at",
                table: "job_runs",
                column: "started_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_type",
                table: "job_runs",
                column: "type");

            migrationBuilder.AddForeignKey(
                name: "fk_events_job_runs_job_run_id",
                table: "events",
                column: "job_run_id",
                principalTable: "job_runs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_events_strikes_strike_id",
                table: "events",
                column: "strike_id",
                principalTable: "strikes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_manual_events_job_runs_job_run_id",
                table: "manual_events",
                column: "job_run_id",
                principalTable: "job_runs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_strikes_job_runs_job_run_id",
                table: "strikes",
                column: "job_run_id",
                principalTable: "job_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_job_runs_job_run_id",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "fk_events_strikes_strike_id",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "fk_manual_events_job_runs_job_run_id",
                table: "manual_events");

            migrationBuilder.DropForeignKey(
                name: "fk_strikes_job_runs_job_run_id",
                table: "strikes");

            migrationBuilder.DropTable(
                name: "job_runs");

            migrationBuilder.DropIndex(
                name: "ix_strikes_job_run_id",
                table: "strikes");

            migrationBuilder.DropIndex(
                name: "ix_manual_events_instance_type",
                table: "manual_events");

            migrationBuilder.DropIndex(
                name: "ix_manual_events_job_run_id",
                table: "manual_events");

            migrationBuilder.DropIndex(
                name: "ix_events_download_client_type",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_instance_type",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_job_run_id",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_strike_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "job_run_id",
                table: "strikes");

            migrationBuilder.DropColumn(
                name: "download_client_name",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "download_client_type",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "instance_type",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "instance_url",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "job_run_id",
                table: "manual_events");

            migrationBuilder.DropColumn(
                name: "download_client_name",
                table: "events");

            migrationBuilder.DropColumn(
                name: "download_client_type",
                table: "events");

            migrationBuilder.DropColumn(
                name: "instance_type",
                table: "events");

            migrationBuilder.DropColumn(
                name: "instance_url",
                table: "events");

            migrationBuilder.DropColumn(
                name: "job_run_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "strike_id",
                table: "events");
        }
    }
}
