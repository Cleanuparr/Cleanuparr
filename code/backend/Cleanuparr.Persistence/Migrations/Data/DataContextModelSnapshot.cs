﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Cleanuparr.Persistence.Migrations.Data
{
    [DbContext(typeof(DataContext))]
    partial class DataContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.6");

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.Arr.ArrConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<short>("FailedImportMaxStrikes")
                        .HasColumnType("INTEGER")
                        .HasColumnName("failed_import_max_strikes");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("type");

                    b.HasKey("Id")
                        .HasName("pk_arr_configs");

                    b.ToTable("arr_configs", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.Arr.ArrInstance", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<string>("ApiKey")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("api_key");

                    b.Property<Guid>("ArrConfigId")
                        .HasColumnType("TEXT")
                        .HasColumnName("arr_config_id");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("enabled");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("url");

                    b.HasKey("Id")
                        .HasName("pk_arr_instances");

                    b.HasIndex("ArrConfigId")
                        .HasDatabaseName("ix_arr_instances_arr_config_id");

                    b.ToTable("arr_instances", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.ContentBlocker.ContentBlockerConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<string>("CronExpression")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("cron_expression");

                    b.Property<bool>("DeleteKnownMalware")
                        .HasColumnType("INTEGER")
                        .HasColumnName("delete_known_malware");

                    b.Property<bool>("DeletePrivate")
                        .HasColumnType("INTEGER")
                        .HasColumnName("delete_private");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("enabled");

                    b.Property<bool>("IgnorePrivate")
                        .HasColumnType("INTEGER")
                        .HasColumnName("ignore_private");

                    b.Property<bool>("UseAdvancedScheduling")
                        .HasColumnType("INTEGER")
                        .HasColumnName("use_advanced_scheduling");

                    b.ComplexProperty<Dictionary<string, object>>("Lidarr", "Cleanuparr.Persistence.Models.Configuration.ContentBlocker.ContentBlockerConfig.Lidarr#BlocklistSettings", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<string>("BlocklistPath")
                                .HasColumnType("TEXT")
                                .HasColumnName("lidarr_blocklist_path");

                            b1.Property<string>("BlocklistType")
                                .IsRequired()
                                .HasColumnType("TEXT")
                                .HasColumnName("lidarr_blocklist_type");

                            b1.Property<bool>("Enabled")
                                .HasColumnType("INTEGER")
                                .HasColumnName("lidarr_enabled");
                        });

                    b.ComplexProperty<Dictionary<string, object>>("Radarr", "Cleanuparr.Persistence.Models.Configuration.ContentBlocker.ContentBlockerConfig.Radarr#BlocklistSettings", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<string>("BlocklistPath")
                                .HasColumnType("TEXT")
                                .HasColumnName("radarr_blocklist_path");

                            b1.Property<string>("BlocklistType")
                                .IsRequired()
                                .HasColumnType("TEXT")
                                .HasColumnName("radarr_blocklist_type");

                            b1.Property<bool>("Enabled")
                                .HasColumnType("INTEGER")
                                .HasColumnName("radarr_enabled");
                        });

                    b.ComplexProperty<Dictionary<string, object>>("Readarr", "Cleanuparr.Persistence.Models.Configuration.ContentBlocker.ContentBlockerConfig.Readarr#BlocklistSettings", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<string>("BlocklistPath")
                                .HasColumnType("TEXT")
                                .HasColumnName("readarr_blocklist_path");

                            b1.Property<string>("BlocklistType")
                                .IsRequired()
                                .HasColumnType("TEXT")
                                .HasColumnName("readarr_blocklist_type");

                            b1.Property<bool>("Enabled")
                                .HasColumnType("INTEGER")
                                .HasColumnName("readarr_enabled");
                        });

                    b.ComplexProperty<Dictionary<string, object>>("Sonarr", "Cleanuparr.Persistence.Models.Configuration.ContentBlocker.ContentBlockerConfig.Sonarr#BlocklistSettings", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<string>("BlocklistPath")
                                .HasColumnType("TEXT")
                                .HasColumnName("sonarr_blocklist_path");

                            b1.Property<string>("BlocklistType")
                                .IsRequired()
                                .HasColumnType("TEXT")
                                .HasColumnName("sonarr_blocklist_type");

                            b1.Property<bool>("Enabled")
                                .HasColumnType("INTEGER")
                                .HasColumnName("sonarr_enabled");
                        });

                    b.ComplexProperty<Dictionary<string, object>>("Whisparr", "Cleanuparr.Persistence.Models.Configuration.ContentBlocker.ContentBlockerConfig.Whisparr#BlocklistSettings", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<string>("BlocklistPath")
                                .HasColumnType("TEXT")
                                .HasColumnName("whisparr_blocklist_path");

                            b1.Property<int>("BlocklistType")
                                .HasColumnType("INTEGER")
                                .HasColumnName("whisparr_blocklist_type");

                            b1.Property<bool>("Enabled")
                                .HasColumnType("INTEGER")
                                .HasColumnName("whisparr_enabled");
                        });

                    b.HasKey("Id")
                        .HasName("pk_content_blocker_configs");

                    b.ToTable("content_blocker_configs", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.DownloadCleaner.CleanCategory", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<Guid>("DownloadCleanerConfigId")
                        .HasColumnType("TEXT")
                        .HasColumnName("download_cleaner_config_id");

                    b.Property<double>("MaxRatio")
                        .HasColumnType("REAL")
                        .HasColumnName("max_ratio");

                    b.Property<double>("MaxSeedTime")
                        .HasColumnType("REAL")
                        .HasColumnName("max_seed_time");

                    b.Property<double>("MinSeedTime")
                        .HasColumnType("REAL")
                        .HasColumnName("min_seed_time");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.HasKey("Id")
                        .HasName("pk_clean_categories");

                    b.HasIndex("DownloadCleanerConfigId")
                        .HasDatabaseName("ix_clean_categories_download_cleaner_config_id");

                    b.ToTable("clean_categories", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.DownloadCleaner.DownloadCleanerConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<string>("CronExpression")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("cron_expression");

                    b.Property<bool>("DeletePrivate")
                        .HasColumnType("INTEGER")
                        .HasColumnName("delete_private");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("enabled");

                    b.PrimitiveCollection<string>("UnlinkedCategories")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("unlinked_categories");

                    b.Property<bool>("UnlinkedEnabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("unlinked_enabled");

                    b.Property<string>("UnlinkedIgnoredRootDir")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("unlinked_ignored_root_dir");

                    b.Property<string>("UnlinkedTargetCategory")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("unlinked_target_category");

                    b.Property<bool>("UnlinkedUseTag")
                        .HasColumnType("INTEGER")
                        .HasColumnName("unlinked_use_tag");

                    b.Property<bool>("UseAdvancedScheduling")
                        .HasColumnType("INTEGER")
                        .HasColumnName("use_advanced_scheduling");

                    b.HasKey("Id")
                        .HasName("pk_download_cleaner_configs");

                    b.ToTable("download_cleaner_configs", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.DownloadClientConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("enabled");

                    b.Property<string>("Host")
                        .HasColumnType("TEXT")
                        .HasColumnName("host");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.Property<string>("Password")
                        .HasColumnType("TEXT")
                        .HasColumnName("password");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("type");

                    b.Property<string>("TypeName")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("type_name");

                    b.Property<string>("UrlBase")
                        .HasColumnType("TEXT")
                        .HasColumnName("url_base");

                    b.Property<string>("Username")
                        .HasColumnType("TEXT")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_download_clients");

                    b.ToTable("download_clients", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.General.GeneralConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<bool>("DisplaySupportBanner")
                        .HasColumnType("INTEGER")
                        .HasColumnName("display_support_banner");

                    b.Property<bool>("DryRun")
                        .HasColumnType("INTEGER")
                        .HasColumnName("dry_run");

                    b.Property<string>("EncryptionKey")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("encryption_key");

                    b.Property<string>("HttpCertificateValidation")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("http_certificate_validation");

                    b.Property<ushort>("HttpMaxRetries")
                        .HasColumnType("INTEGER")
                        .HasColumnName("http_max_retries");

                    b.Property<ushort>("HttpTimeout")
                        .HasColumnType("INTEGER")
                        .HasColumnName("http_timeout");

                    b.PrimitiveCollection<string>("IgnoredDownloads")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("ignored_downloads");

                    b.Property<string>("LogLevel")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("log_level");

                    b.Property<ushort>("SearchDelay")
                        .HasColumnType("INTEGER")
                        .HasColumnName("search_delay");

                    b.Property<bool>("SearchEnabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("search_enabled");

                    b.HasKey("Id")
                        .HasName("pk_general_configs");

                    b.ToTable("general_configs", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.Notification.AppriseConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<string>("FullUrl")
                        .HasColumnType("TEXT")
                        .HasColumnName("full_url");

                    b.Property<string>("Key")
                        .HasColumnType("TEXT")
                        .HasColumnName("key");

                    b.Property<bool>("OnCategoryChanged")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_category_changed");

                    b.Property<bool>("OnDownloadCleaned")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_download_cleaned");

                    b.Property<bool>("OnFailedImportStrike")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_failed_import_strike");

                    b.Property<bool>("OnQueueItemDeleted")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_queue_item_deleted");

                    b.Property<bool>("OnSlowStrike")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_slow_strike");

                    b.Property<bool>("OnStalledStrike")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_stalled_strike");

                    b.Property<string>("Tags")
                        .HasColumnType("TEXT")
                        .HasColumnName("tags");

                    b.HasKey("Id")
                        .HasName("pk_apprise_configs");

                    b.ToTable("apprise_configs", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.Notification.NotifiarrConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<string>("ApiKey")
                        .HasColumnType("TEXT")
                        .HasColumnName("api_key");

                    b.Property<string>("ChannelId")
                        .HasColumnType("TEXT")
                        .HasColumnName("channel_id");

                    b.Property<bool>("OnCategoryChanged")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_category_changed");

                    b.Property<bool>("OnDownloadCleaned")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_download_cleaned");

                    b.Property<bool>("OnFailedImportStrike")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_failed_import_strike");

                    b.Property<bool>("OnQueueItemDeleted")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_queue_item_deleted");

                    b.Property<bool>("OnSlowStrike")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_slow_strike");

                    b.Property<bool>("OnStalledStrike")
                        .HasColumnType("INTEGER")
                        .HasColumnName("on_stalled_strike");

                    b.HasKey("Id")
                        .HasName("pk_notifiarr_configs");

                    b.ToTable("notifiarr_configs", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.QueueCleaner.QueueCleanerConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<string>("CronExpression")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("cron_expression");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("enabled");

                    b.Property<bool>("UseAdvancedScheduling")
                        .HasColumnType("INTEGER")
                        .HasColumnName("use_advanced_scheduling");

                    b.ComplexProperty<Dictionary<string, object>>("FailedImport", "Cleanuparr.Persistence.Models.Configuration.QueueCleaner.QueueCleanerConfig.FailedImport#FailedImportConfig", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<bool>("DeletePrivate")
                                .HasColumnType("INTEGER")
                                .HasColumnName("failed_import_delete_private");

                            b1.Property<bool>("IgnorePrivate")
                                .HasColumnType("INTEGER")
                                .HasColumnName("failed_import_ignore_private");

                            b1.PrimitiveCollection<string>("IgnoredPatterns")
                                .IsRequired()
                                .HasColumnType("TEXT")
                                .HasColumnName("failed_import_ignored_patterns");

                            b1.Property<ushort>("MaxStrikes")
                                .HasColumnType("INTEGER")
                                .HasColumnName("failed_import_max_strikes");
                        });

                    b.ComplexProperty<Dictionary<string, object>>("Slow", "Cleanuparr.Persistence.Models.Configuration.QueueCleaner.QueueCleanerConfig.Slow#SlowConfig", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<bool>("DeletePrivate")
                                .HasColumnType("INTEGER")
                                .HasColumnName("slow_delete_private");

                            b1.Property<string>("IgnoreAboveSize")
                                .IsRequired()
                                .HasColumnType("TEXT")
                                .HasColumnName("slow_ignore_above_size");

                            b1.Property<bool>("IgnorePrivate")
                                .HasColumnType("INTEGER")
                                .HasColumnName("slow_ignore_private");

                            b1.Property<ushort>("MaxStrikes")
                                .HasColumnType("INTEGER")
                                .HasColumnName("slow_max_strikes");

                            b1.Property<double>("MaxTime")
                                .HasColumnType("REAL")
                                .HasColumnName("slow_max_time");

                            b1.Property<string>("MinSpeed")
                                .IsRequired()
                                .HasColumnType("TEXT")
                                .HasColumnName("slow_min_speed");

                            b1.Property<bool>("ResetStrikesOnProgress")
                                .HasColumnType("INTEGER")
                                .HasColumnName("slow_reset_strikes_on_progress");
                        });

                    b.ComplexProperty<Dictionary<string, object>>("Stalled", "Cleanuparr.Persistence.Models.Configuration.QueueCleaner.QueueCleanerConfig.Stalled#StalledConfig", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<bool>("DeletePrivate")
                                .HasColumnType("INTEGER")
                                .HasColumnName("stalled_delete_private");

                            b1.Property<ushort>("DownloadingMetadataMaxStrikes")
                                .HasColumnType("INTEGER")
                                .HasColumnName("stalled_downloading_metadata_max_strikes");

                            b1.Property<bool>("IgnorePrivate")
                                .HasColumnType("INTEGER")
                                .HasColumnName("stalled_ignore_private");

                            b1.Property<ushort>("MaxStrikes")
                                .HasColumnType("INTEGER")
                                .HasColumnName("stalled_max_strikes");

                            b1.Property<bool>("ResetStrikesOnProgress")
                                .HasColumnType("INTEGER")
                                .HasColumnName("stalled_reset_strikes_on_progress");
                        });

                    b.HasKey("Id")
                        .HasName("pk_queue_cleaner_configs");

                    b.ToTable("queue_cleaner_configs", (string)null);
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.Arr.ArrInstance", b =>
                {
                    b.HasOne("Cleanuparr.Persistence.Models.Configuration.Arr.ArrConfig", "ArrConfig")
                        .WithMany("Instances")
                        .HasForeignKey("ArrConfigId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_arr_instances_arr_configs_arr_config_id");

                    b.Navigation("ArrConfig");
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.DownloadCleaner.CleanCategory", b =>
                {
                    b.HasOne("Cleanuparr.Persistence.Models.Configuration.DownloadCleaner.DownloadCleanerConfig", "DownloadCleanerConfig")
                        .WithMany("Categories")
                        .HasForeignKey("DownloadCleanerConfigId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_clean_categories_download_cleaner_configs_download_cleaner_config_id");

                    b.Navigation("DownloadCleanerConfig");
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.Arr.ArrConfig", b =>
                {
                    b.Navigation("Instances");
                });

            modelBuilder.Entity("Cleanuparr.Persistence.Models.Configuration.DownloadCleaner.DownloadCleanerConfig", b =>
                {
                    b.Navigation("Categories");
                });
#pragma warning restore 612, 618
        }
    }
}
