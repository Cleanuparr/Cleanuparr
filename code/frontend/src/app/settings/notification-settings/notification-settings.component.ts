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
import { NotifiarrFormData, AppriseFormData, NtfyFormData } from "./models/provider-modal.model";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";

// New modal components
import { ProviderTypeSelectionComponent } from "./modals/provider-type-selection/provider-type-selection.component";
import { NotifiarrProviderComponent } from "./modals/notifiarr-provider/notifiarr-provider.component";
import { AppriseProviderComponent } from "./modals/apprise-provider/apprise-provider.component";
import { NtfyProviderComponent } from "./modals/ntfy-provider/ntfy-provider.component";

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
  showTypeSelectionModal = false; // New: Provider type selection modal
  showNotifiarrModal = false; // New: Notifiarr provider modal
  showAppriseModal = false; // New: Apprise provider modal
  showNtfyModal = false; // New: Ntfy provider modal
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

    // Setup effect to react to test results
    effect(() => {
      const result = this.testResult();
      if (result) {
        if (result.success) {
          this.notificationService.showSuccess(result.message || "Test notification sent successfully");
        } else {
          // Error handling is already done in the test error effect above
          // This just handles the success case
        }
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
          url: appriseConfig.url,
          key: appriseConfig.key,
          tags: appriseConfig.tags || "",
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
      url: data.url,
      key: data.key,
      tags: data.tags,
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
      url: data.url,
      key: data.key,
      tags: data.tags,
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
      url: data.url,
      key: data.key,
      tags: data.tags,
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
