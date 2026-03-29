using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddPerClientDownloadCleanerSettings : Migration
    {
        /// <inheritdoc />
        private const string NewGuid = "lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',abs(random())%4+1,1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6)))";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create new tables first (before dropping old data)
            migrationBuilder.CreateTable(
                name: "deluge_seeding_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deluge_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_deluge_seeding_rules_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "q_bit_seeding_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_q_bit_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_q_bit_seeding_rules_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "r_torrent_seeding_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_r_torrent_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_r_torrent_seeding_rules_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transmission_seeding_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transmission_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_transmission_seeding_rules_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "u_torrent_seeding_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_u_torrent_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_u_torrent_seeding_rules_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unlinked_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    target_category = table.Column<string>(type: "TEXT", nullable: false),
                    use_tag = table.Column<bool>(type: "INTEGER", nullable: false),
                    ignored_root_dirs = table.Column<string>(type: "TEXT", nullable: false),
                    categories = table.Column<string>(type: "TEXT", nullable: false),
                    download_directory_source = table.Column<string>(type: "TEXT", nullable: true),
                    download_directory_target = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unlinked_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_unlinked_configs_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deluge_seeding_rules_download_client_config_id",
                table: "deluge_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_q_bit_seeding_rules_download_client_config_id",
                table: "q_bit_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_r_torrent_seeding_rules_download_client_config_id",
                table: "r_torrent_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_transmission_seeding_rules_download_client_config_id",
                table: "transmission_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_u_torrent_seeding_rules_download_client_config_id",
                table: "u_torrent_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_unlinked_configs_download_client_config_id",
                table: "unlinked_configs",
                column: "download_client_config_id",
                unique: true);

            // 2. Migrate existing seeding rules to per-client tables
            // For each download client, copy all global seeding rules to the per-type table matching the client's type_name
            migrationBuilder.Sql($@"
                INSERT INTO q_bit_seeding_rules (id, download_client_config_id, name, privacy_type, max_ratio, min_seed_time, max_seed_time, delete_source_files)
                SELECT {NewGuid}, dc.id, sr.name, sr.privacy_type, sr.max_ratio, sr.min_seed_time, sr.max_seed_time, sr.delete_source_files
                FROM download_clients dc
                CROSS JOIN seeding_rules sr
                WHERE dc.type_name = 'qbittorrent';
            ");

            migrationBuilder.Sql($@"
                INSERT INTO deluge_seeding_rules (id, download_client_config_id, name, privacy_type, max_ratio, min_seed_time, max_seed_time, delete_source_files)
                SELECT {NewGuid}, dc.id, sr.name, sr.privacy_type, sr.max_ratio, sr.min_seed_time, sr.max_seed_time, sr.delete_source_files
                FROM download_clients dc
                CROSS JOIN seeding_rules sr
                WHERE dc.type_name = 'deluge';
            ");

            migrationBuilder.Sql($@"
                INSERT INTO transmission_seeding_rules (id, download_client_config_id, name, privacy_type, max_ratio, min_seed_time, max_seed_time, delete_source_files)
                SELECT {NewGuid}, dc.id, sr.name, sr.privacy_type, sr.max_ratio, sr.min_seed_time, sr.max_seed_time, sr.delete_source_files
                FROM download_clients dc
                CROSS JOIN seeding_rules sr
                WHERE dc.type_name = 'transmission';
            ");

            migrationBuilder.Sql($@"
                INSERT INTO u_torrent_seeding_rules (id, download_client_config_id, name, privacy_type, max_ratio, min_seed_time, max_seed_time, delete_source_files)
                SELECT {NewGuid}, dc.id, sr.name, sr.privacy_type, sr.max_ratio, sr.min_seed_time, sr.max_seed_time, sr.delete_source_files
                FROM download_clients dc
                CROSS JOIN seeding_rules sr
                WHERE dc.type_name = 'utorrent';
            ");

            migrationBuilder.Sql($@"
                INSERT INTO r_torrent_seeding_rules (id, download_client_config_id, name, privacy_type, max_ratio, min_seed_time, max_seed_time, delete_source_files)
                SELECT {NewGuid}, dc.id, sr.name, sr.privacy_type, sr.max_ratio, sr.min_seed_time, sr.max_seed_time, sr.delete_source_files
                FROM download_clients dc
                CROSS JOIN seeding_rules sr
                WHERE dc.type_name = 'rtorrent';
            ");

            // 3. Migrate unlinked config for each download client
            migrationBuilder.Sql($@"
                INSERT INTO unlinked_configs (id, download_client_config_id, enabled, target_category, use_tag, ignored_root_dirs, categories)
                SELECT {NewGuid}, dc.id, dcc.unlinked_enabled, dcc.unlinked_target_category, dcc.unlinked_use_tag, dcc.unlinked_ignored_root_dirs, dcc.unlinked_categories
                FROM download_clients dc
                CROSS JOIN download_cleaner_configs dcc;
            ");

            // 4. Drop old tables and columns
            migrationBuilder.DropTable(
                name: "seeding_rules");

            migrationBuilder.DropColumn(
                name: "unlinked_categories",
                table: "download_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "unlinked_enabled",
                table: "download_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "unlinked_ignored_root_dirs",
                table: "download_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "unlinked_target_category",
                table: "download_cleaner_configs");

            migrationBuilder.DropColumn(
                name: "unlinked_use_tag",
                table: "download_cleaner_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deluge_seeding_rules");

            migrationBuilder.DropTable(
                name: "q_bit_seeding_rules");

            migrationBuilder.DropTable(
                name: "r_torrent_seeding_rules");

            migrationBuilder.DropTable(
                name: "transmission_seeding_rules");

            migrationBuilder.DropTable(
                name: "u_torrent_seeding_rules");

            migrationBuilder.DropTable(
                name: "unlinked_configs");

            migrationBuilder.AddColumn<string>(
                name: "unlinked_categories",
                table: "download_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "unlinked_enabled",
                table: "download_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "unlinked_ignored_root_dirs",
                table: "download_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "unlinked_target_category",
                table: "download_cleaner_configs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "unlinked_use_tag",
                table: "download_cleaner_configs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "seeding_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    download_cleaner_config_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    delete_source_files = table.Column<bool>(type: "INTEGER", nullable: false),
                    max_ratio = table.Column<double>(type: "REAL", nullable: false),
                    max_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    min_seed_time = table.Column<double>(type: "REAL", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    privacy_type = table.Column<string>(type: "TEXT", nullable: false)
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
        }
    }
}
