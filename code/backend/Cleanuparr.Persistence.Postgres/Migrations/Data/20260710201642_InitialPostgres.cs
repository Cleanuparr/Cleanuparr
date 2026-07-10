using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cleanuparr.Persistence.Postgres.Migrations.Data
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "data");

            migrationBuilder.CreateTable(
                name: "arr_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    failed_import_max_strikes = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arr_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blacklist_sync_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    cron_expression = table.Column<string>(type: "text", nullable: false),
                    blacklist_path = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blacklist_sync_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_blocker_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    cron_expression = table.Column<string>(type: "text", nullable: false),
                    use_advanced_scheduling = table.Column<bool>(type: "boolean", nullable: false),
                    ignore_private = table.Column<bool>(type: "boolean", nullable: false),
                    delete_private = table.Column<bool>(type: "boolean", nullable: false),
                    process_no_content_id = table.Column<bool>(type: "boolean", nullable: false),
                    delete_if_any_file_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    ignored_downloads = table.Column<List<string>>(type: "text[]", nullable: false),
                    lidarr_blocklist_path = table.Column<string>(type: "text", nullable: true),
                    lidarr_blocklist_type = table.Column<string>(type: "text", nullable: false),
                    lidarr_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    radarr_blocklist_path = table.Column<string>(type: "text", nullable: true),
                    radarr_blocklist_type = table.Column<string>(type: "text", nullable: false),
                    radarr_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    readarr_blocklist_path = table.Column<string>(type: "text", nullable: true),
                    readarr_blocklist_type = table.Column<string>(type: "text", nullable: false),
                    readarr_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sonarr_blocklist_path = table.Column<string>(type: "text", nullable: true),
                    sonarr_blocklist_type = table.Column<string>(type: "text", nullable: false),
                    sonarr_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    whisparr_blocklist_path = table.Column<string>(type: "text", nullable: true),
                    whisparr_blocklist_type = table.Column<int>(type: "integer", nullable: false),
                    whisparr_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_blocker_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "download_cleaner_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    cron_expression = table.Column<string>(type: "text", nullable: false),
                    use_advanced_scheduling = table.Column<bool>(type: "boolean", nullable: false),
                    ignored_downloads = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_download_cleaner_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "download_clients",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type_name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    host = table.Column<string>(type: "text", nullable: true),
                    username = table.Column<string>(type: "text", nullable: true),
                    password = table.Column<string>(type: "text", nullable: true),
                    url_base = table.Column<string>(type: "text", nullable: true),
                    external_url = table.Column<string>(type: "text", nullable: true),
                    download_directory_source = table.Column<string>(type: "text", nullable: true),
                    download_directory_target = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_download_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "general_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_support_banner = table.Column<bool>(type: "boolean", nullable: false),
                    dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    http_max_retries = table.Column<int>(type: "integer", nullable: false),
                    http_timeout = table.Column<int>(type: "integer", nullable: false),
                    http_certificate_validation = table.Column<string>(type: "text", nullable: false),
                    status_check_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    encryption_key = table.Column<string>(type: "text", nullable: false),
                    ignored_downloads = table.Column<List<string>>(type: "text[]", nullable: false),
                    strike_inactivity_window_hours = table.Column<int>(type: "integer", nullable: false),
                    history_retention_days = table.Column<int>(type: "integer", nullable: false),
                    auth_disable_auth_for_local_addresses = table.Column<bool>(type: "boolean", nullable: false),
                    auth_trust_forwarded_headers = table.Column<bool>(type: "boolean", nullable: false),
                    auth_trusted_networks = table.Column<string>(type: "text", nullable: false),
                    log_archive_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    log_archive_retained_count = table.Column<int>(type: "integer", nullable: false),
                    log_archive_time_limit_hours = table.Column<int>(type: "integer", nullable: false),
                    log_level = table.Column<string>(type: "text", nullable: false),
                    log_retained_file_count = table.Column<int>(type: "integer", nullable: false),
                    log_rolling_size_mb = table.Column<int>(type: "integer", nullable: false),
                    log_time_limit_hours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_general_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    on_failed_import_strike = table.Column<bool>(type: "boolean", nullable: false),
                    on_stalled_strike = table.Column<bool>(type: "boolean", nullable: false),
                    on_slow_strike = table.Column<bool>(type: "boolean", nullable: false),
                    on_queue_item_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    on_download_cleaned = table.Column<bool>(type: "boolean", nullable: false),
                    on_category_changed = table.Column<bool>(type: "boolean", nullable: false),
                    on_search_triggered = table.Column<bool>(type: "boolean", nullable: false),
                    on_search_item_grabbed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "queue_cleaner_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    cron_expression = table.Column<string>(type: "text", nullable: false),
                    use_advanced_scheduling = table.Column<bool>(type: "boolean", nullable: false),
                    ignored_downloads = table.Column<List<string>>(type: "text[]", nullable: false),
                    process_no_content_id = table.Column<bool>(type: "boolean", nullable: false),
                    downloading_metadata_max_strikes = table.Column<int>(type: "integer", nullable: false),
                    failed_import_change_category = table.Column<bool>(type: "boolean", nullable: false),
                    failed_import_delete_private = table.Column<bool>(type: "boolean", nullable: false),
                    failed_import_ignore_private = table.Column<bool>(type: "boolean", nullable: false),
                    failed_import_max_strikes = table.Column<int>(type: "integer", nullable: false),
                    failed_import_pattern_mode = table.Column<string>(type: "text", nullable: false),
                    failed_import_patterns = table.Column<string[]>(type: "text[]", nullable: false),
                    failed_import_skip_if_not_found_in_client = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_cleaner_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seeker_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    search_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    search_interval = table.Column<int>(type: "integer", nullable: false),
                    proactive_search_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    selection_strategy = table.Column<string>(type: "text", nullable: false),
                    use_round_robin = table.Column<bool>(type: "boolean", nullable: false),
                    post_release_grace_hours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "arr_instances",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<float>(type: "real", nullable: false),
                    arr_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    external_url = table.Column<string>(type: "text", nullable: true),
                    api_key = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arr_instances", x => x.id);
                    table.ForeignKey(
                        name: "fk_arr_instances_arr_configs_arr_config_id",
                        column: x => x.arr_config_id,
                        principalSchema: "data",
                        principalTable: "arr_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blacklist_sync_history",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    download_client_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blacklist_sync_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_blacklist_sync_history_download_clients_download_client_id",
                        column: x => x.download_client_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dead_torrent_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    target_category = table.Column<string>(type: "text", nullable: false),
                    use_tag = table.Column<bool>(type: "boolean", nullable: false),
                    max_strikes = table.Column<int>(type: "integer", nullable: false),
                    categories = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dead_torrent_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_dead_torrent_configs_download_clients_download_client_confi",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deluge_seeding_rules",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    categories = table.Column<string>(type: "text", nullable: false),
                    tracker_patterns = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    privacy_type = table.Column<string>(type: "text", nullable: false),
                    max_ratio = table.Column<double>(type: "double precision", nullable: false),
                    min_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    max_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    min_seeders = table.Column<int>(type: "integer", nullable: false),
                    delete_source_files = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deluge_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_deluge_seeding_rules_download_clients_download_client_confi",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orphaned_files_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    scan_directories = table.Column<string>(type: "text", nullable: false),
                    orphaned_directory = table.Column<string>(type: "text", nullable: false),
                    exclude_patterns = table.Column<string>(type: "text", nullable: false),
                    min_file_age_hours = table.Column<int>(type: "integer", nullable: false),
                    purge_after_hours = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orphaned_files_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_orphaned_files_configs_download_clients_download_client_con",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "q_bit_seeding_rules",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    categories = table.Column<string>(type: "text", nullable: false),
                    tracker_patterns = table.Column<string>(type: "text", nullable: false),
                    tags_any = table.Column<string>(type: "text", nullable: false),
                    tags_all = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    privacy_type = table.Column<string>(type: "text", nullable: false),
                    max_ratio = table.Column<double>(type: "double precision", nullable: false),
                    min_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    max_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    min_seeders = table.Column<int>(type: "integer", nullable: false),
                    delete_source_files = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_q_bit_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_q_bit_seeding_rules_download_clients_download_client_config",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "r_torrent_seeding_rules",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    categories = table.Column<string>(type: "text", nullable: false),
                    tracker_patterns = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    privacy_type = table.Column<string>(type: "text", nullable: false),
                    max_ratio = table.Column<double>(type: "double precision", nullable: false),
                    min_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    max_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    delete_source_files = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_r_torrent_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_r_torrent_seeding_rules_download_clients_download_client_co",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transmission_seeding_rules",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    categories = table.Column<string>(type: "text", nullable: false),
                    tracker_patterns = table.Column<string>(type: "text", nullable: false),
                    tags_any = table.Column<string>(type: "text", nullable: false),
                    tags_all = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    privacy_type = table.Column<string>(type: "text", nullable: false),
                    max_ratio = table.Column<double>(type: "double precision", nullable: false),
                    min_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    max_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    min_seeders = table.Column<int>(type: "integer", nullable: false),
                    delete_source_files = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transmission_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_transmission_seeding_rules_download_clients_download_client",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "u_torrent_seeding_rules",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    categories = table.Column<string>(type: "text", nullable: false),
                    tracker_patterns = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    privacy_type = table.Column<string>(type: "text", nullable: false),
                    max_ratio = table.Column<double>(type: "double precision", nullable: false),
                    min_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    max_seed_time = table.Column<double>(type: "double precision", nullable: false),
                    min_seeders = table.Column<int>(type: "integer", nullable: false),
                    delete_source_files = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_u_torrent_seeding_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_u_torrent_seeding_rules_download_clients_download_client_co",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unlinked_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    download_client_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    target_category = table.Column<string>(type: "text", nullable: false),
                    use_tag = table.Column<bool>(type: "boolean", nullable: false),
                    ignored_root_dirs = table.Column<List<string>>(type: "text[]", nullable: false),
                    categories = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unlinked_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_unlinked_configs_download_clients_download_client_config_id",
                        column: x => x.download_client_config_id,
                        principalSchema: "data",
                        principalTable: "download_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "apprise_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tags = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    service_urls = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_apprise_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_apprise_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalSchema: "data",
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "discord_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    username = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discord_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_discord_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalSchema: "data",
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gotify_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    application_token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gotify_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_gotify_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalSchema: "data",
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifiarr_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    channel_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifiarr_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifiarr_configs_notification_configs_notification_config_",
                        column: x => x.notification_config_id,
                        principalSchema: "data",
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ntfy_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    topics = table.Column<List<string>>(type: "text[]", nullable: false),
                    authentication_type = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    access_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    priority = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ntfy_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_ntfy_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalSchema: "data",
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pushover_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    devices = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: false),
                    sound = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    retry = table.Column<int>(type: "integer", nullable: true),
                    expire = table.Column<int>(type: "integer", nullable: true),
                    tags = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pushover_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_pushover_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalSchema: "data",
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telegram_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bot_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    chat_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    topic_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    send_silently = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telegram_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_telegram_configs_notification_configs_notification_config_id",
                        column: x => x.notification_config_id,
                        principalSchema: "data",
                        principalTable: "notification_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "slow_rules",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reset_strikes_on_progress = table.Column<bool>(type: "boolean", nullable: false),
                    max_time_hours = table.Column<double>(type: "double precision", nullable: false),
                    min_speed = table.Column<string>(type: "text", nullable: false),
                    ignore_above_size = table.Column<string>(type: "text", nullable: true),
                    queue_cleaner_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    max_strikes = table.Column<int>(type: "integer", nullable: false),
                    privacy_type = table.Column<string>(type: "text", nullable: false),
                    min_completion_percentage = table.Column<int>(type: "integer", nullable: false),
                    max_completion_percentage = table.Column<int>(type: "integer", nullable: false),
                    delete_private_torrents_from_client = table.Column<bool>(type: "boolean", nullable: false),
                    change_category = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slow_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_slow_rules_queue_cleaner_configs_queue_cleaner_config_id",
                        column: x => x.queue_cleaner_config_id,
                        principalSchema: "data",
                        principalTable: "queue_cleaner_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stall_rules",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reset_strikes_on_progress = table.Column<bool>(type: "boolean", nullable: false),
                    minimum_progress = table.Column<string>(type: "text", nullable: true),
                    queue_cleaner_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    max_strikes = table.Column<int>(type: "integer", nullable: false),
                    privacy_type = table.Column<string>(type: "text", nullable: false),
                    min_completion_percentage = table.Column<int>(type: "integer", nullable: false),
                    max_completion_percentage = table.Column<int>(type: "integer", nullable: false),
                    delete_private_torrents_from_client = table.Column<bool>(type: "boolean", nullable: false),
                    change_category = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stall_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_stall_rules_queue_cleaner_configs_queue_cleaner_config_id",
                        column: x => x.queue_cleaner_config_id,
                        principalSchema: "data",
                        principalTable: "queue_cleaner_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seeker_instance_configs",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arr_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    skip_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    last_processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_eligible_items = table.Column<int>(type: "integer", nullable: false),
                    active_download_limit = table.Column<int>(type: "integer", nullable: false),
                    min_cycle_time_days = table.Column<int>(type: "integer", nullable: false),
                    monitored_only = table.Column<bool>(type: "boolean", nullable: false),
                    use_cutoff = table.Column<bool>(type: "boolean", nullable: false),
                    use_custom_format_score = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seeker_instance_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_seeker_instance_configs_arr_instances_arr_instance_id",
                        column: x => x.arr_instance_id,
                        principalSchema: "data",
                        principalTable: "arr_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_apprise_configs_notification_config_id",
                schema: "data",
                table: "apprise_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_arr_instances_arr_config_id",
                schema: "data",
                table: "arr_instances",
                column: "arr_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_sync_history_download_client_id",
                schema: "data",
                table: "blacklist_sync_history",
                column: "download_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_sync_history_hash",
                schema: "data",
                table: "blacklist_sync_history",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_sync_history_hash_download_client_id",
                schema: "data",
                table: "blacklist_sync_history",
                columns: new[] { "hash", "download_client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dead_torrent_configs_download_client_config_id",
                schema: "data",
                table: "dead_torrent_configs",
                column: "download_client_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deluge_seeding_rules_download_client_config_id",
                schema: "data",
                table: "deluge_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_discord_configs_notification_config_id",
                schema: "data",
                table: "discord_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gotify_configs_notification_config_id",
                schema: "data",
                table: "gotify_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifiarr_configs_notification_config_id",
                schema: "data",
                table: "notifiarr_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_configs_name",
                schema: "data",
                table: "notification_configs",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ntfy_configs_notification_config_id",
                schema: "data",
                table: "ntfy_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orphaned_files_configs_download_client_config_id",
                schema: "data",
                table: "orphaned_files_configs",
                column: "download_client_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pushover_configs_notification_config_id",
                schema: "data",
                table: "pushover_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_q_bit_seeding_rules_download_client_config_id",
                schema: "data",
                table: "q_bit_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_r_torrent_seeding_rules_download_client_config_id",
                schema: "data",
                table: "r_torrent_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_seeker_instance_configs_arr_instance_id",
                schema: "data",
                table: "seeker_instance_configs",
                column: "arr_instance_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_slow_rules_queue_cleaner_config_id",
                schema: "data",
                table: "slow_rules",
                column: "queue_cleaner_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_stall_rules_queue_cleaner_config_id",
                schema: "data",
                table: "stall_rules",
                column: "queue_cleaner_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_telegram_configs_notification_config_id",
                schema: "data",
                table: "telegram_configs",
                column: "notification_config_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transmission_seeding_rules_download_client_config_id",
                schema: "data",
                table: "transmission_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_u_torrent_seeding_rules_download_client_config_id",
                schema: "data",
                table: "u_torrent_seeding_rules",
                column: "download_client_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_unlinked_configs_download_client_config_id",
                schema: "data",
                table: "unlinked_configs",
                column: "download_client_config_id",
                unique: true);

            migrationBuilder.InsertData(
                schema: "data",
                table: "general_configs",
                columns: new[]
                {
                    "id", "display_support_banner", "dry_run", "http_max_retries", "http_timeout", "http_certificate_validation",
                    "status_check_enabled", "encryption_key", "ignored_downloads", "strike_inactivity_window_hours", "history_retention_days",
                    "auth_disable_auth_for_local_addresses", "auth_trust_forwarded_headers", "auth_trusted_networks",
                    "log_archive_enabled", "log_archive_retained_count", "log_archive_time_limit_hours", "log_level",
                    "log_retained_file_count", "log_rolling_size_mb", "log_time_limit_hours",
                },
                values: new object[]
                {
                    Guid.NewGuid(), true, false, 0, 100, "enabled",
                    true, Guid.NewGuid().ToString(), Array.Empty<string>(), 24, 365,
                    false, false, "",
                    true, 60, 720, "information",
                    5, 10, 24,
                });

            migrationBuilder.InsertData(
                schema: "data",
                table: "queue_cleaner_configs",
                columns: new[]
                {
                    "id", "enabled", "cron_expression", "use_advanced_scheduling", "ignored_downloads", "process_no_content_id",
                    "downloading_metadata_max_strikes", "failed_import_change_category", "failed_import_delete_private",
                    "failed_import_ignore_private", "failed_import_max_strikes", "failed_import_pattern_mode",
                    "failed_import_patterns", "failed_import_skip_if_not_found_in_client",
                },
                values: new object[]
                {
                    Guid.NewGuid(), false, "0 0/5 * * * ?", false, Array.Empty<string>(), false,
                    0, false, false,
                    false, 0, "include",
                    Array.Empty<string>(), true,
                });

            migrationBuilder.InsertData(
                schema: "data",
                table: "content_blocker_configs",
                columns: new[]
                {
                    "id", "enabled", "cron_expression", "use_advanced_scheduling", "ignore_private", "delete_private",
                    "process_no_content_id", "delete_if_any_file_blocked",
                    "sonarr_enabled", "sonarr_blocklist_path", "sonarr_blocklist_type",
                    "radarr_enabled", "radarr_blocklist_path", "radarr_blocklist_type",
                    "lidarr_enabled", "lidarr_blocklist_path", "lidarr_blocklist_type",
                    "readarr_enabled", "readarr_blocklist_path", "readarr_blocklist_type",
                    "whisparr_enabled", "whisparr_blocklist_path", "whisparr_blocklist_type",
                    "ignored_downloads",
                },
                values: new object[]
                {
                    Guid.NewGuid(), false, "0/5 * * * * ?", false, false, false,
                    false, false,
                    false, null, "blacklist",
                    false, null, "blacklist",
                    false, null, "blacklist",
                    false, null, "blacklist",
                    false, null, 0,
                    Array.Empty<string>(),
                });

            migrationBuilder.InsertData(
                schema: "data",
                table: "download_cleaner_configs",
                columns: new[] { "id", "enabled", "cron_expression", "use_advanced_scheduling", "ignored_downloads" },
                values: new object[] { Guid.NewGuid(), false, "0 0 * * * ?", false, Array.Empty<string>() });

            migrationBuilder.InsertData(
                schema: "data",
                table: "seeker_configs",
                columns: new[]
                {
                    "id", "search_enabled", "search_interval", "proactive_search_enabled",
                    "selection_strategy", "use_round_robin", "post_release_grace_hours",
                },
                values: new object[] { Guid.NewGuid(), true, 10, false, "balancedweighted", true, 6 });

            migrationBuilder.InsertData(
                schema: "data",
                table: "blacklist_sync_configs",
                columns: new[] { "id", "enabled", "cron_expression", "blacklist_path" },
                values: new object[] { Guid.NewGuid(), false, "0 0 * * * ?", null });

            migrationBuilder.InsertData(
                schema: "data",
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { Guid.NewGuid(), (short)-1, "radarr" });

            migrationBuilder.InsertData(
                schema: "data",
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { Guid.NewGuid(), (short)-1, "lidarr" });

            migrationBuilder.InsertData(
                schema: "data",
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { Guid.NewGuid(), (short)-1, "sonarr" });

            migrationBuilder.InsertData(
                schema: "data",
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { Guid.NewGuid(), (short)-1, "readarr" });

            migrationBuilder.InsertData(
                schema: "data",
                table: "arr_configs",
                columns: new[] { "id", "failed_import_max_strikes", "type" },
                values: new object[] { Guid.NewGuid(), (short)-1, "whisparr" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apprise_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "blacklist_sync_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "blacklist_sync_history",
                schema: "data");

            migrationBuilder.DropTable(
                name: "content_blocker_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "dead_torrent_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "deluge_seeding_rules",
                schema: "data");

            migrationBuilder.DropTable(
                name: "discord_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "download_cleaner_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "general_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "gotify_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "notifiarr_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "ntfy_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "orphaned_files_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "pushover_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "q_bit_seeding_rules",
                schema: "data");

            migrationBuilder.DropTable(
                name: "r_torrent_seeding_rules",
                schema: "data");

            migrationBuilder.DropTable(
                name: "seeker_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "seeker_instance_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "slow_rules",
                schema: "data");

            migrationBuilder.DropTable(
                name: "stall_rules",
                schema: "data");

            migrationBuilder.DropTable(
                name: "telegram_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "transmission_seeding_rules",
                schema: "data");

            migrationBuilder.DropTable(
                name: "u_torrent_seeding_rules",
                schema: "data");

            migrationBuilder.DropTable(
                name: "unlinked_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "arr_instances",
                schema: "data");

            migrationBuilder.DropTable(
                name: "queue_cleaner_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "notification_configs",
                schema: "data");

            migrationBuilder.DropTable(
                name: "download_clients",
                schema: "data");

            migrationBuilder.DropTable(
                name: "arr_configs",
                schema: "data");
        }
    }
}
