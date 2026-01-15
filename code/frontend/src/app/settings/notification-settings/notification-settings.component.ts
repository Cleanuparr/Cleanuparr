import { Component, EventEmitter, OnDestroy, Output, effect, inject, computed } from "@angular/core";
import { CommonModule } from "@angular/common";
import { Subject, takeUntil } from "rxjs";
import { NotificationProviderConfigStore } from "../notification-provider/notification-provider-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { 
  NotificationProviderDto
} from "../../shared/models/notification-provider.model";
import { NotificationProviderType } from "../../shared/models/enums";
import { DocumentationService } from "../../core/services/documentation.service";
import { NotifiarrFormData, AppriseFormData, NtfyFormData, PushoverFormData, TelegramFormData, DiscordFormData, GotifyFormData } from "./models/provider-modal.model";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";

// New modal components
import { ProviderTypeSelectionComponent } from "./modals/provider-type-selection/provider-type-selection.component";
import { NotifiarrProviderComponent } from "./modals/notifiarr-provider/notifiarr-provider.component";
import { AppriseProviderComponent } from "./modals/apprise-provider/apprise-provider.component";
import { NtfyProviderComponent } from "./modals/ntfy-provider/ntfy-provider.component";
import { PushoverProviderComponent } from "./modals/pushover-provider/pushover-provider.component";
import { TelegramProviderComponent } from "./modals/telegram-provider/telegram-provider.component";
import { DiscordProviderComponent } from "./modals/discord-provider/discord-provider.component";
import { GotifyProviderComponent } from "./modals/gotify-provider/gotify-provider.component";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { SelectModule } from 'primeng/select';
import { ToastModule } from "primeng/toast";
import { DialogModule } from "primeng/dialog";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { TagModule } from "primeng/tag";
import { TooltipModule } from "primeng/tooltip";
import { ConfirmationService, MessageService } from "primeng/api";
import { NotificationService } from "../../core/services/notification.service";

@Component({
  selector: "app-notification-settings",
  standalone: true,
  imports: [
    CommonModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    SelectModule,
    ToastModule,
    DialogModule,
    ConfirmDialogModule,
    TagModule,
    TooltipModule,
    LoadingErrorStateComponent,
    ProviderTypeSelectionComponent,
    NotifiarrProviderComponent,
    AppriseProviderComponent,
    NtfyProviderComponent,
    PushoverProviderComponent,
    TelegramProviderComponent,
    DiscordProviderComponent,
    GotifyProviderComponent,
  ],
  providers: [NotificationProviderConfigStore, ConfirmationService, MessageService],
  templateUrl: "./notification-settings.component.html",
  styleUrls: ["./notification-settings.component.scss"],
})
export class NotificationSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Modal state
  showProviderModal = false; // Legacy modal for unsupported types
  showTypeSelectionModal = false;
  showNotifiarrModal = false;
  showAppriseModal = false;
  showNtfyModal = false;
  showPushoverModal = false;
  showTelegramModal = false;
  showDiscordModal = false;
  showGotifyModal = false;
  modalMode: 'add' | 'edit' = 'add';
  editingProvider: NotificationProviderDto | null = null;

  get isEditing(): boolean {
    return this.modalMode === 'edit';
  }

  // Clean up subscriptions
  private destroy$ = new Subject<void>();

  // Services
  private notificationService = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);
  public readonly notificationProviderStore = inject(NotificationProviderConfigStore);
  private documentationService = inject(DocumentationService);

  // Signals from store
  notificationProviderConfig = this.notificationProviderStore.config();
  notificationProviderLoading = this.notificationProviderStore.loading;
  notificationProviderLoadError = this.notificationProviderStore.loadError; // Only for "Not connected" state
  notificationProviderSaveError = this.notificationProviderStore.saveError; // Only for toast notifications
  notificationProviderTestError = this.notificationProviderStore.testError; // Only for toast notifications
  notificationProviderSaving = this.notificationProviderStore.saving;
  notificationProviderTesting = this.notificationProviderStore.testing;
  testResult = this.notificationProviderStore.testResult;

  saving = computed(() => this.notificationProviderSaving());
  testing = computed(() => this.notificationProviderTesting());

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return true; // No unsaved changes in modal-based approach
  }

  constructor() {
    // Store will auto-load data via onInit hook

    // Effect to handle load errors - emit to LoadingErrorStateComponent for "Not connected" display
    effect(() => {
      const loadErrorMessage = this.notificationProviderLoadError();
      if (loadErrorMessage) {
        // Emit to parent component which will show LoadingErrorStateComponent
        this.error.emit(loadErrorMessage);
      }
    });
    
    // Effect: show test errors as toast
    effect(() => {
      const testErrorMessage = this.notificationProviderTestError();
      if (testErrorMessage) {
        // Test errors should always be shown as toast notifications
        this.notificationService.showError(testErrorMessage);

        // Clear the error after handling
        this.notificationProviderStore.resetTestError();
      }
    });

    // Setup effect to react to test results (HTTP 200 = success)
    effect(() => {
      const result = this.testResult();
      if (result) {
        this.notificationService.showSuccess(result.message || "Test notification sent successfully");
      }
    });
  }

  /**
   * Clean up subscriptions when component is destroyed
   */
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Open modal to add new provider - starts with type selection
   */
  openAddProviderModal(): void {
    this.modalMode = "add";
    this.editingProvider = null;
    this.showTypeSelectionModal = true; // New: Show type selection first
  }

  /**
   * Open modal to edit existing provider
   */
  openEditProviderModal(provider: NotificationProviderDto): void {
    // Close all modals first to ensure clean state
    this.closeAllModals();

    this.modalMode = "edit";
    this.editingProvider = provider;

    // Open the appropriate provider-specific modal based on type
    switch (provider.type) {
      case NotificationProviderType.Notifiarr:
        this.showNotifiarrModal = true;
        break;
      case NotificationProviderType.Apprise:
        this.showAppriseModal = true;
        break;
      case NotificationProviderType.Ntfy:
        this.showNtfyModal = true;
        break;
      case NotificationProviderType.Pushover:
        this.showPushoverModal = true;
        break;
      case NotificationProviderType.Telegram:
        this.showTelegramModal = true;
        break;
      case NotificationProviderType.Discord:
        this.showDiscordModal = true;
        break;
      case NotificationProviderType.Gotify:
        this.showGotifyModal = true;
        break;
      default:
        // For unsupported types, show the legacy modal with info message
        this.showProviderModal = true;
        break;
    }
  }

  /**
   * Close provider modal
   */
  closeProviderModal(): void {
    this.showProviderModal = false;
    this.editingProvider = null;
    this.notificationProviderStore.clearTestResult();
  }

  /**
   * Handle provider type selection from type selection modal
   */
  onProviderTypeSelected(type: NotificationProviderType): void {
    this.showTypeSelectionModal = false;
    this.openProviderSpecificModal(type);
  }

  /**
   * Handle type selection modal cancel
   */
  onTypeSelectionCancel(): void {
    this.showTypeSelectionModal = false;
  }

  /**
   * Open provider-specific modal based on type
   */
  private openProviderSpecificModal(type: NotificationProviderType): void {
    // Reset editing state for new provider
    this.editingProvider = null;
    this.modalMode = "add";

    // Open the appropriate provider-specific modal
    switch (type) {
      case NotificationProviderType.Notifiarr:
        this.showNotifiarrModal = true;
        break;
      case NotificationProviderType.Apprise:
        this.showAppriseModal = true;
        break;
      case NotificationProviderType.Ntfy:
        this.showNtfyModal = true;
        break;
      case NotificationProviderType.Pushover:
        this.showPushoverModal = true;
        break;
      case NotificationProviderType.Telegram:
        this.showTelegramModal = true;
        break;
      case NotificationProviderType.Discord:
        this.showDiscordModal = true;
        break;
      case NotificationProviderType.Gotify:
        this.showGotifyModal = true;
        break;
      default:
        // For unsupported types, show the legacy modal with info message
        this.showProviderModal = true;
        break;
    }
  }

  /**
   * Delete provider with confirmation
   */
  deleteProvider(provider: NotificationProviderDto): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to delete the provider "${provider.name}"?`,
      header: "Confirm Deletion",
      icon: "pi pi-exclamation-triangle",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.notificationProviderStore.deleteProvider(provider.id);

        // Reuse monitor for success/error handling
        this.monitorProviderOperation('deleted');
      },
    });
  }

  /**
   * Test notification provider
   */
  testProvider(provider: NotificationProviderDto): void {
    // Build test request based on provider type
    let testRequest: any;

    switch (provider.type) {
      case NotificationProviderType.Notifiarr:
        const notifiarrConfig = provider.configuration as any;
        testRequest = {
          apiKey: notifiarrConfig.apiKey,
          channelId: notifiarrConfig.channelId,
        };
        break;
      case NotificationProviderType.Apprise:
        const appriseConfig = provider.configuration as any;
        testRequest = {
          mode: appriseConfig.mode,
          url: appriseConfig.url || "",
          key: appriseConfig.key || "",
          tags: appriseConfig.tags || "",
          serviceUrls: appriseConfig.serviceUrls || "",
        };
        break;
      case NotificationProviderType.Ntfy:
        const ntfyConfig = provider.configuration as any;
        testRequest = {
          serverUrl: ntfyConfig.serverUrl,
          topics: ntfyConfig.topics,
          authenticationType: ntfyConfig.authenticationType,
          username: ntfyConfig.username || "",
          password: ntfyConfig.password || "",
          accessToken: ntfyConfig.accessToken || "",
          priority: ntfyConfig.priority,
          tags: ntfyConfig.tags || "",
        };
        break;
      case NotificationProviderType.Pushover:
        const pushoverConfig = provider.configuration as any;
        testRequest = {
          apiToken: pushoverConfig.apiToken,
          userKey: pushoverConfig.userKey,
          devices: pushoverConfig.devices || [],
          priority: pushoverConfig.priority,
          sound: pushoverConfig.sound || "",
          retry: pushoverConfig.retry,
          expire: pushoverConfig.expire,
          tags: pushoverConfig.tags || [],
        };
        break;
      case NotificationProviderType.Telegram:
        const telegramConfig = provider.configuration as any;
        testRequest = {
          botToken: telegramConfig.botToken,
          chatId: telegramConfig.chatId,
          topicId: telegramConfig.topicId || "",
          sendSilently: telegramConfig.sendSilently || false,
        };
        break;
      case NotificationProviderType.Discord:
        const discordConfig = provider.configuration as any;
        testRequest = {
          webhookUrl: discordConfig.webhookUrl,
          username: discordConfig.username || "",
          avatarUrl: discordConfig.avatarUrl || "",
        };
        break;
      case NotificationProviderType.Gotify:
        const gotifyConfig = provider.configuration as any;
        testRequest = {
          serverUrl: gotifyConfig.serverUrl,
          applicationToken: gotifyConfig.applicationToken,
          priority: gotifyConfig.priority ?? 5,
        };
        break;
      default:
        this.notificationService.showError("Testing not supported for this provider type");
        return;
    }

    this.notificationProviderStore.testProvider({
      testRequest,
      type: provider.type,
    });
  }

  /**
   * Test notification provider from modal
   */
  testProviderFromModal(): void {
    if (this.editingProvider) {
      this.testProvider(this.editingProvider);
    }
  }

  /**
   * Get modal title based on mode
   */
  get modalTitle(): string {
    return this.modalMode === "add" ? "Add Notification Provider" : "Edit Notification Provider";
  }

  /**
   * Get provider type label for display
   */
  getProviderTypeLabel(type: NotificationProviderType): string {
    switch (type) {
      case NotificationProviderType.Notifiarr:
        return "Notifiarr";
      case NotificationProviderType.Apprise:
        return "Apprise";
      case NotificationProviderType.Ntfy:
        return "ntfy";
      case NotificationProviderType.Pushover:
        return "Pushover";
      case NotificationProviderType.Telegram:
        return "Telegram";
      case NotificationProviderType.Discord:
        return "Discord";
      case NotificationProviderType.Gotify:
        return "Gotify";
      default:
        return "Unknown";
    }
  }

  /**
   * Get provider type label for an existing provider
   */
  getProviderTypeLabelForProvider(provider: NotificationProviderDto): string {
    return this.getProviderTypeLabel(provider.type);
  }

  /**
   * Open field-specific documentation
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation("notifications", fieldName);
  }

  // Provider modal handlers

  /**
   * Handle Notifiarr provider save
   */
  onNotifiarrSave(data: NotifiarrFormData): void {
    if (this.modalMode === "edit" && this.editingProvider) {
      this.updateNotifiarrProvider(data);
    } else {
      this.createNotifiarrProvider(data);
    }
  }

  /**
   * Handle Notifiarr provider test
   */
  onNotifiarrTest(data: NotifiarrFormData): void {
    const testRequest = {
      apiKey: data.apiKey,
      channelId: data.channelId,
    };

    this.notificationProviderStore.testProvider({
      testRequest,
      type: NotificationProviderType.Notifiarr,
    });
  }

  /**
   * Handle Apprise provider save
   */
  onAppriseSave(data: AppriseFormData): void {
    if (this.modalMode === "edit" && this.editingProvider) {
      this.updateAppriseProvider(data);
    } else {
      this.createAppriseProvider(data);
    }
  }

  /**
   * Handle Apprise provider test
   */
  onAppriseTest(data: AppriseFormData): void {
    const testRequest = {
      mode: data.mode,
      url: data.url,
      key: data.key,
      tags: data.tags,
      serviceUrls: data.serviceUrls,
    };

    this.notificationProviderStore.testProvider({
      testRequest,
      type: NotificationProviderType.Apprise,
    });
  }

  /**
   * Handle Ntfy provider save
   */
  onNtfySave(data: NtfyFormData): void {
    if (this.modalMode === "edit" && this.editingProvider) {
      this.updateNtfyProvider(data);
    } else {
      this.createNtfyProvider(data);
    }
  }

  /**
   * Handle Ntfy provider test
   */
  onNtfyTest(data: NtfyFormData): void {
    const testRequest = {
      serverUrl: data.serverUrl,
      topics: data.topics,
      authenticationType: data.authenticationType,
      username: data.username,
      password: data.password,
      accessToken: data.accessToken,
      priority: data.priority,
      tags: data.tags,
    };

    this.notificationProviderStore.testProvider({
      testRequest,
      type: NotificationProviderType.Ntfy,
    });
  }

  /**
   * Handle Pushover provider save
   */
  onPushoverSave(data: PushoverFormData): void {
    if (this.modalMode === "edit" && this.editingProvider) {
      this.updatePushoverProvider(data);
    } else {
      this.createPushoverProvider(data);
    }
  }

  /**
   * Handle Pushover provider test
   */
  onPushoverTest(data: PushoverFormData): void {
    const testRequest = {
      apiToken: data.apiToken,
      userKey: data.userKey,
      devices: data.devices,
      priority: data.priority,
      sound: data.sound,
      retry: data.retry,
      expire: data.expire,
      tags: data.tags,
    };

    this.notificationProviderStore.testProvider({
      testRequest,
      type: NotificationProviderType.Pushover,
    });
  }

  /**
   * Handle Telegram provider save
   */
  onTelegramSave(data: TelegramFormData): void {
    if (this.modalMode === "edit" && this.editingProvider) {
      this.updateTelegramProvider(data);
    } else {
      this.createTelegramProvider(data);
    }
  }

  /**
   * Handle Telegram provider test
   */
  onTelegramTest(data: TelegramFormData): void {
    const testRequest = {
      botToken: data.botToken,
      chatId: data.chatId,
      topicId: data.topicId,
      sendSilently: data.sendSilently,
    };

    this.notificationProviderStore.testProvider({
      testRequest,
      type: NotificationProviderType.Telegram,
    });
  }

  /**
   * Handle Discord provider save
   */
  onDiscordSave(data: DiscordFormData): void {
    if (this.modalMode === "edit" && this.editingProvider) {
      this.updateDiscordProvider(data);
    } else {
      this.createDiscordProvider(data);
    }
  }

  /**
   * Handle Discord provider test
   */
  onDiscordTest(data: DiscordFormData): void {
    const testRequest = {
      webhookUrl: data.webhookUrl,
      username: data.username,
      avatarUrl: data.avatarUrl,
    };

    this.notificationProviderStore.testProvider({
      testRequest,
      type: NotificationProviderType.Discord,
    });
  }

  /**
   * Handle Gotify provider save
   */
  onGotifySave(data: GotifyFormData): void {
    if (this.modalMode === "edit" && this.editingProvider) {
      this.updateGotifyProvider(data);
    } else {
      this.createGotifyProvider(data);
    }
  }

  /**
   * Handle Gotify provider test
   */
  onGotifyTest(data: GotifyFormData): void {
    const testRequest = {
      serverUrl: data.serverUrl,
      applicationToken: data.applicationToken,
      priority: data.priority,
    };

    this.notificationProviderStore.testProvider({
      testRequest,
      type: NotificationProviderType.Gotify,
    });
  }

  /**
   * Handle provider modal cancel
   */
  onProviderCancel(): void {
    this.closeAllModals();
  }

  /**
   * Close all provider modals
   */
  private closeAllModals(): void {
    this.showTypeSelectionModal = false;
    this.showNotifiarrModal = false;
    this.showAppriseModal = false;
    this.showNtfyModal = false;
    this.showPushoverModal = false;
    this.showTelegramModal = false;
    this.showDiscordModal = false;
    this.showGotifyModal = false;
    this.showProviderModal = false;
    this.editingProvider = null;
    this.notificationProviderStore.clearTestResult();
  }

  /**
   * Create new Notifiarr provider
   */
  private createNotifiarrProvider(data: NotifiarrFormData): void {
    const createDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      apiKey: data.apiKey,
      channelId: data.channelId,
    };

    this.notificationProviderStore.createProvider({
      provider: createDto,
      type: NotificationProviderType.Notifiarr,
    });
    this.monitorProviderOperation("created");
  }

  /**
   * Update existing Notifiarr provider
   */
  private updateNotifiarrProvider(data: NotifiarrFormData): void {
    if (!this.editingProvider) return;

    const updateDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      apiKey: data.apiKey,
      channelId: data.channelId,
    };

    this.notificationProviderStore.updateProvider({
      id: this.editingProvider.id,
      provider: updateDto,
      type: NotificationProviderType.Notifiarr,
    });
    this.monitorProviderOperation("updated");
  }

  /**
   * Create new Apprise provider
   */
  private createAppriseProvider(data: AppriseFormData): void {
    const createDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      mode: data.mode,
      url: data.url,
      key: data.key,
      tags: data.tags,
      serviceUrls: data.serviceUrls,
    };

    this.notificationProviderStore.createProvider({
      provider: createDto,
      type: NotificationProviderType.Apprise,
    });
    this.monitorProviderOperation("created");
  }

  /**
   * Update existing Apprise provider
   */
  private updateAppriseProvider(data: AppriseFormData): void {
    if (!this.editingProvider) return;

    const updateDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      mode: data.mode,
      url: data.url,
      key: data.key,
      tags: data.tags,
      serviceUrls: data.serviceUrls,
    };

    this.notificationProviderStore.updateProvider({
      id: this.editingProvider.id,
      provider: updateDto,
      type: NotificationProviderType.Apprise,
    });
    this.monitorProviderOperation("updated");
  }

  /**
   * Create new Ntfy provider
   */
  private createNtfyProvider(data: NtfyFormData): void {
    const createDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      serverUrl: data.serverUrl,
      topics: data.topics,
      authenticationType: data.authenticationType,
      username: data.username,
      password: data.password,
      accessToken: data.accessToken,
      priority: data.priority,
      tags: data.tags,
    };

    this.notificationProviderStore.createProvider({
      provider: createDto,
      type: NotificationProviderType.Ntfy,
    });
    this.monitorProviderOperation("created");
  }

  /**
   * Update existing Ntfy provider
   */
  private updateNtfyProvider(data: NtfyFormData): void {
    if (!this.editingProvider) return;

    const updateDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      serverUrl: data.serverUrl,
      topics: data.topics,
      authenticationType: data.authenticationType,
      username: data.username,
      password: data.password,
      accessToken: data.accessToken,
      priority: data.priority,
      tags: data.tags,
    };

    this.notificationProviderStore.updateProvider({
      id: this.editingProvider.id,
      provider: updateDto,
      type: NotificationProviderType.Ntfy,
    });
    this.monitorProviderOperation("updated");
  }

  /**
   * Create new Pushover provider
   */
  private createPushoverProvider(data: PushoverFormData): void {
    const createDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      apiToken: data.apiToken,
      userKey: data.userKey,
      devices: data.devices,
      priority: data.priority,
      sound: data.sound,
      retry: data.retry,
      expire: data.expire,
      tags: data.tags,
    };

    this.notificationProviderStore.createProvider({
      provider: createDto,
      type: NotificationProviderType.Pushover,
    });
    this.monitorProviderOperation("created");
  }

  /**
   * Update existing Pushover provider
   */
  private updatePushoverProvider(data: PushoverFormData): void {
    if (!this.editingProvider) return;

    const updateDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      apiToken: data.apiToken,
      userKey: data.userKey,
      devices: data.devices,
      priority: data.priority,
      sound: data.sound,
      retry: data.retry,
      expire: data.expire,
      tags: data.tags,
    };

    this.notificationProviderStore.updateProvider({
      id: this.editingProvider.id,
      provider: updateDto,
      type: NotificationProviderType.Pushover,
    });
    this.monitorProviderOperation("updated");
  }

  /**
   * Create new Telegram provider
   */
  private createTelegramProvider(data: TelegramFormData): void {
    const createDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      botToken: data.botToken,
      chatId: data.chatId,
      topicId: data.topicId,
      sendSilently: data.sendSilently,
    };

    this.notificationProviderStore.createProvider({
      provider: createDto,
      type: NotificationProviderType.Telegram,
    });
    this.monitorProviderOperation("created");
  }

  /**
   * Update existing Telegram provider
   */
  private updateTelegramProvider(data: TelegramFormData): void {
    if (!this.editingProvider) return;

    const updateDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      botToken: data.botToken,
      chatId: data.chatId,
      topicId: data.topicId,
      sendSilently: data.sendSilently,
    };

    this.notificationProviderStore.updateProvider({
      id: this.editingProvider.id,
      provider: updateDto,
      type: NotificationProviderType.Telegram,
    });
    this.monitorProviderOperation("updated");
  }

  /**
   * Create new Discord provider
   */
  private createDiscordProvider(data: DiscordFormData): void {
    const createDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      webhookUrl: data.webhookUrl,
      username: data.username,
      avatarUrl: data.avatarUrl,
    };

    this.notificationProviderStore.createProvider({
      provider: createDto,
      type: NotificationProviderType.Discord,
    });
    this.monitorProviderOperation("created");
  }

  /**
   * Update existing Discord provider
   */
  private updateDiscordProvider(data: DiscordFormData): void {
    if (!this.editingProvider) return;

    const updateDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      webhookUrl: data.webhookUrl,
      username: data.username,
      avatarUrl: data.avatarUrl,
    };

    this.notificationProviderStore.updateProvider({
      id: this.editingProvider.id,
      provider: updateDto,
      type: NotificationProviderType.Discord,
    });
    this.monitorProviderOperation("updated");
  }

  /**
   * Create new Gotify provider
   */
  private createGotifyProvider(data: GotifyFormData): void {
    const createDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      serverUrl: data.serverUrl,
      applicationToken: data.applicationToken,
      priority: data.priority,
    };

    this.notificationProviderStore.createProvider({
      provider: createDto,
      type: NotificationProviderType.Gotify,
    });
    this.monitorProviderOperation("created");
  }

  /**
   * Update existing Gotify provider
   */
  private updateGotifyProvider(data: GotifyFormData): void {
    if (!this.editingProvider) return;

    const updateDto = {
      name: data.name,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      serverUrl: data.serverUrl,
      applicationToken: data.applicationToken,
      priority: data.priority,
    };

    this.notificationProviderStore.updateProvider({
      id: this.editingProvider.id,
      provider: updateDto,
      type: NotificationProviderType.Gotify,
    });
    this.monitorProviderOperation("updated");
  }

  /**
   * Monitor provider operation completion and close modals
   */
  private monitorProviderOperation(operation: string): void {
    const checkStatus = () => {
      const saving = this.notificationProviderSaving();
      const saveError = this.notificationProviderSaveError();

      if (!saving) {
        if (saveError) {
          // Show error once and clear it
          this.notificationService.showError(saveError);
          this.notificationProviderStore.resetSaveError();
        } else {
          // Operation completed successfully
          this.notificationService.showSuccess(`Provider ${operation} successfully`);
          this.closeAllModals();
        }
      } else {
        // Still saving, check again
        setTimeout(checkStatus, 100);
      }
    };

    setTimeout(checkStatus, 100);
  }
} 
