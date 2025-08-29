import { Component, EventEmitter, OnDestroy, Output, effect, inject, computed } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { NotificationProviderConfigStore } from "../notification-provider/notification-provider-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { 
  NotificationProviderDto, 
  CreateNotificationProviderDto, 
  UpdateNotificationProviderDto 
} from "../../shared/models/notification-provider.model";
import { NotificationProviderType } from "../../shared/models/enums";
import { DocumentationService } from "../../core/services/documentation.service";
import { NotifiarrFormData, AppriseFormData } from "./models/provider-modal.model";

// New modal components
import { ProviderTypeSelectionComponent } from "./modals/provider-type-selection/provider-type-selection.component";
import { NotifiarrProviderComponent } from "./modals/notifiarr-provider/notifiarr-provider.component";
import { AppriseProviderComponent } from "./modals/apprise-provider/apprise-provider.component";

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
    ReactiveFormsModule,
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
    ProviderTypeSelectionComponent,
    NotifiarrProviderComponent,
    AppriseProviderComponent
  ],
  providers: [NotificationProviderConfigStore, ConfirmationService, MessageService],
  templateUrl: "./notification-settings.component.html",
  styleUrls: ["./notification-settings.component.scss"],
})
export class NotificationSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Forms
  providerForm: FormGroup;

  // Modal state
  showProviderModal = false; // Keep old modal for now during transition
  showTypeSelectionModal = false; // New: Provider type selection modal
  showNotifiarrModal = false; // New: Notifiarr provider modal
  showAppriseModal = false; // New: Apprise provider modal
  modalMode: 'add' | 'edit' = 'add';
  editingProvider: NotificationProviderDto | null = null;

  // Notification provider type options
  typeOptions: { label: string, value: NotificationProviderType }[] = [];

  get isEditing(): boolean {
    return this.modalMode === 'edit';
  }

  // Clean up subscriptions
  private destroy$ = new Subject<void>();

  // Services
  private formBuilder = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);
  private notificationProviderStore = inject(NotificationProviderConfigStore);
  private documentationService = inject(DocumentationService);

  // Signals from store
  notificationProviderConfig = this.notificationProviderStore.config;
  notificationProviderLoading = this.notificationProviderStore.loading;
  notificationProviderError = this.notificationProviderStore.error;
  notificationProviderSaving = this.notificationProviderStore.saving;
  notificationProviderTesting = this.notificationProviderStore.testing;
  testResult = this.notificationProviderStore.testResult;

  // Computed signals
  providers = computed(() => this.notificationProviderConfig()?.providers || []);
  saving = computed(() => this.notificationProviderSaving());
  testing = computed(() => this.notificationProviderTesting());

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return true; // No unsaved changes in modal-based approach
  }

  constructor() {
    // Initialize provider form for modal
    this.providerForm = this.formBuilder.group({
      name: ['', Validators.required],
      type: [null, Validators.required],
      enabled: [true],
      // Configuration fields that will be dynamically shown based on type
      apiKey: [''],
      channelId: [''],
      fullUrl: [''],
      key: [''],
      tags: [''],
      // Event trigger flags
      onFailedImportStrike: [false],
      onStalledStrike: [false],
      onSlowStrike: [false],
      onQueueItemDeleted: [false],
      onDownloadCleaned: [false],
      onCategoryChanged: [false]
    });

    // Initialize type options
    for (const key of Object.keys(NotificationProviderType)) {
      this.typeOptions.push({ 
        label: this.getProviderTypeLabel(NotificationProviderType[key as keyof typeof NotificationProviderType]), 
        value: NotificationProviderType[key as keyof typeof NotificationProviderType] 
      });
    }

    // Load notification provider config data
    this.notificationProviderStore.loadConfig();

    // Setup provider type change handler
    this.providerForm.get('type')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.onProviderTypeChange();
      });

    // Setup effect to react to test results
    effect(() => {
      const result = this.testResult();
      if (result) {
        if (result.success) {
          this.notificationService.showSuccess(result.message || 'Test notification sent successfully');
        } else {
          this.notificationService.showError(result.message || 'Test notification failed');
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
   * Mark all controls in a form group as touched
   */
  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.values(formGroup.controls).forEach((control) => {
      control.markAsTouched();

      if ((control as any).controls) {
        this.markFormGroupTouched(control as FormGroup);
      }
    });
  }

  /**
   * Check if a form control has an error
   */
  hasError(form: FormGroup, controlName: string, errorName: string): boolean {
    const control = form.get(controlName);
    return control !== null && control.hasError(errorName) && control.dirty;
  }

  /**
   * Open modal to add new provider - starts with type selection
   */
  openAddProviderModal(): void {
    this.modalMode = 'add';
    this.editingProvider = null;
    this.showTypeSelectionModal = true; // New: Show type selection first
  }

  /**
   * Open modal to edit existing provider
   */
  openEditProviderModal(provider: NotificationProviderDto): void {
    this.modalMode = 'edit';
    this.editingProvider = provider;
    this.showProviderModal = true;
    
    // Extract configuration based on type
    let notifiarrConfig: any = null;
    let appriseConfig: any = null;
    
    if (provider.type === NotificationProviderType.Notifiarr) {
      notifiarrConfig = provider.configuration;
    } else if (provider.type === NotificationProviderType.Apprise) {
      appriseConfig = provider.configuration;
    }
    
    this.providerForm.patchValue({
      name: provider.name,
      type: provider.type,
      enabled: provider.isEnabled,
      // Notifiarr fields
      apiKey: notifiarrConfig?.apiKey || '',
      channelId: notifiarrConfig?.channelId || '',
      // Apprise fields
      fullUrl: appriseConfig?.fullUrl || '',
      key: appriseConfig?.key || '',
      tags: appriseConfig?.tags || '',
      // Event flags
      onFailedImportStrike: provider.events.onFailedImportStrike,
      onStalledStrike: provider.events.onStalledStrike,
      onSlowStrike: provider.events.onSlowStrike,
      onQueueItemDeleted: provider.events.onQueueItemDeleted,
      onDownloadCleaned: provider.events.onDownloadCleaned,
      onCategoryChanged: provider.events.onCategoryChanged
    });
    this.showProviderModal = true;
  }

  /**
   * Close provider modal
   */
  closeProviderModal(): void {
    this.showProviderModal = false;
    this.editingProvider = null;
    this.providerForm.reset();
    this.notificationProviderStore.clearTestResult();
  }

  /**
   * Handle provider type selection from type selection modal
   */
  onProviderTypeSelected(type: NotificationProviderType): void {
    this.showTypeSelectionModal = false;
    // TODO: Open provider-specific modal based on type
    // For now, fall back to the old modal (will be replaced in Phase 2)
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
    this.modalMode = 'add';
    
    // Open the appropriate provider-specific modal
    switch (type) {
      case NotificationProviderType.Notifiarr:
        this.showNotifiarrModal = true;
        break;
      case NotificationProviderType.Apprise:
        this.showAppriseModal = true;
        break;
      default:
        // For unsupported types, fall back to the old modal
        this.providerForm.reset();
        this.providerForm.patchValue({ 
          enabled: true,
          type: type
        });
        this.showProviderModal = true;
        break;
    }
  }

  /**
   * Save provider (add or edit)
   */
  saveProvider(): void {
    this.markFormGroupTouched(this.providerForm);

    if (this.providerForm.invalid) {
      this.notificationService.showError('Please fix the validation errors before saving');
      return;
    }

    const formValue = this.providerForm.value;

    if (this.modalMode === 'add') {
      const providerData: CreateNotificationProviderDto = {
        name: formValue.name,
        type: formValue.type,
        isEnabled: formValue.enabled,
        configuration: formValue.type === NotificationProviderType.Notifiarr ? {
          apiKey: formValue.apiKey,
          channelId: formValue.channelId
        } : formValue.type === NotificationProviderType.Apprise ? {
          fullUrl: formValue.fullUrl,
          key: formValue.key,
          tags: formValue.tags
        } : {},
        onFailedImportStrike: formValue.onFailedImportStrike,
        onStalledStrike: formValue.onStalledStrike,
        onSlowStrike: formValue.onSlowStrike,
        onQueueItemDeleted: formValue.onQueueItemDeleted,
        onDownloadCleaned: formValue.onDownloadCleaned,
        onCategoryChanged: formValue.onCategoryChanged
      };
      
      this.notificationProviderStore.createProvider(providerData);
    } else if (this.editingProvider) {
      const providerData: UpdateNotificationProviderDto = {
        name: formValue.name,
        type: formValue.type,
        isEnabled: formValue.enabled,
        configuration: formValue.type === NotificationProviderType.Notifiarr ? {
          apiKey: formValue.apiKey,
          channelId: formValue.channelId
        } : formValue.type === NotificationProviderType.Apprise ? {
          fullUrl: formValue.fullUrl,
          key: formValue.key,
          tags: formValue.tags
        } : {},
        onFailedImportStrike: formValue.onFailedImportStrike,
        onStalledStrike: formValue.onStalledStrike,
        onSlowStrike: formValue.onSlowStrike,
        onQueueItemDeleted: formValue.onQueueItemDeleted,
        onDownloadCleaned: formValue.onDownloadCleaned,
        onCategoryChanged: formValue.onCategoryChanged
      };
      
      this.notificationProviderStore.updateProvider({ 
        id: this.editingProvider.id!, 
        provider: providerData
      });
    }

    this.monitorProviderSaving();
  }

  /**
   * Monitor provider saving completion
   */
  private monitorProviderSaving(): void {
    const checkSavingStatus = () => {
      const saving = this.notificationProviderSaving();
      const error = this.notificationProviderError();
      
      if (!saving) {
        if (error) {
          this.notificationService.showError(`Operation failed: ${error}`);
        } else {
          const action = this.modalMode === 'add' ? 'created' : 'updated';
          this.notificationService.showSuccess(`Provider ${action} successfully`);
          this.closeProviderModal();
        }
      } else {
        setTimeout(checkSavingStatus, 100);
      }
    };
    
    setTimeout(checkSavingStatus, 100);
  }

  /**
   * Delete provider with confirmation
   */
  deleteProvider(provider: NotificationProviderDto): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to delete the provider "${provider.name}"?`,
      header: 'Confirm Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.notificationProviderStore.deleteProvider(provider.id!);
        
        // Monitor deletion
        const checkDeletionStatus = () => {
          const saving = this.notificationProviderSaving();
          const error = this.notificationProviderError();
          
          if (!saving) {
            if (error) {
              this.notificationService.showError(`Deletion failed: ${error}`);
            } else {
              this.notificationService.showSuccess('Provider deleted successfully');
            }
          } else {
            setTimeout(checkDeletionStatus, 100);
          }
        };
        
        setTimeout(checkDeletionStatus, 100);
      }
    });
  }

  /**
   * Test notification provider
   */
  testProvider(provider: NotificationProviderDto): void {
    this.notificationProviderStore.testProvider({ id: provider.id! });
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
    return this.modalMode === 'add' ? 'Add Notification Provider' : 'Edit Notification Provider';
  }

  /**
   * Test current provider (for modal test button)
   */
  testCurrentProvider(): void {
    if (this.editingProvider) {
      this.testProvider(this.editingProvider);
    }
  }

  /**
   * Handle provider type changes to update form validation and visibility
   */
  onTypeChange(event: any): void {
    const type = event.value;
    
    // Clear the form except for basic fields when type changes
    const currentValues = this.providerForm.value;
    this.providerForm.reset();
    this.providerForm.patchValue({
      enabled: currentValues.enabled,
      name: currentValues.name,
      type: type
    });
  }

  /**
   * Handle provider type changes to update form validation and visibility
   */
  onProviderTypeChange(): void {
    const providerType = this.providerForm.get('type')?.value;
    
    // Reset provider-specific fields
    this.providerForm.patchValue({
      apiKey: '',
      channelId: '',
      fullUrl: '',
      key: '',
      tags: ''
    });

    // Set required validators based on type
    const apiKeyControl = this.providerForm.get('apiKey');
    const fullUrlControl = this.providerForm.get('fullUrl');
    
    if (apiKeyControl && fullUrlControl) {
      apiKeyControl.clearValidators();
      fullUrlControl.clearValidators();
      
      if (providerType === NotificationProviderType.Notifiarr) {
        apiKeyControl.setValidators([Validators.required]);
      } else if (providerType === NotificationProviderType.Apprise) {
        fullUrlControl.setValidators([Validators.required]);
      }
      
      apiKeyControl.updateValueAndValidity();
      fullUrlControl.updateValueAndValidity();
    }
  }

  /**
   * Check if Notifiarr fields should be shown
   */
  isNotifiarrType(): boolean {
    return this.providerForm.get('type')?.value === NotificationProviderType.Notifiarr;
  }

  /**
   * Check if Apprise fields should be shown
   */
  isAppriseType(): boolean {
    return this.providerForm.get('type')?.value === NotificationProviderType.Apprise;
  }

  /**
   * Get provider type label for display
   */
  getProviderTypeLabel(type: NotificationProviderType): string {
    switch (type) {
      case NotificationProviderType.Notifiarr:
        return 'Notifiarr';
      case NotificationProviderType.Apprise:
        return 'Apprise';
      default:
        return 'Unknown';
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
    this.documentationService.openFieldDocumentation('notifications', fieldName);
  }

  // Provider-specific modal event handlers

  /**
   * Handle Notifiarr provider save
   */
  onNotifiarrSave(data: NotifiarrFormData): void {
    if (this.modalMode === 'edit' && this.editingProvider) {
      this.updateNotifiarrProvider(data);
    } else {
      this.createNotifiarrProvider(data);
    }
  }

  /**
   * Handle Notifiarr provider test
   */
  onNotifiarrTest(data: NotifiarrFormData): void {
    // TODO: Implement test functionality
    this.messageService.add({
      severity: 'info',
      summary: 'Test',
      detail: 'Notifiarr test functionality coming soon'
    });
  }

  /**
   * Handle Apprise provider save
   */
  onAppriseSave(data: AppriseFormData): void {
    if (this.modalMode === 'edit' && this.editingProvider) {
      this.updateAppriseProvider(data);
    } else {
      this.createAppriseProvider(data);
    }
  }

  /**
   * Handle Apprise provider test
   */
  onAppriseTest(data: AppriseFormData): void {
    // TODO: Implement test functionality
    this.messageService.add({
      severity: 'info',
      summary: 'Test',
      detail: 'Apprise test functionality coming soon'
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
    this.showProviderModal = false;
    this.editingProvider = null;
  }

  /**
   * Create new Notifiarr provider
   */
  private createNotifiarrProvider(data: NotifiarrFormData): void {
    const createDto: CreateNotificationProviderDto = {
      name: data.name,
      type: NotificationProviderType.Notifiarr,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      configuration: {
        apiKey: data.apiKey,
        channelId: data.channelId
      }
    };

    this.notificationProviderStore.createProvider(createDto);
    this.closeAllModals();
  }

  /**
   * Update existing Notifiarr provider
   */
  private updateNotifiarrProvider(data: NotifiarrFormData): void {
    if (!this.editingProvider) return;

    const updateDto: UpdateNotificationProviderDto = {
      name: data.name,
      type: this.editingProvider.type,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      configuration: {
        apiKey: data.apiKey,
        channelId: data.channelId
      }
    };

    this.notificationProviderStore.updateProvider({ 
      id: this.editingProvider.id, 
      provider: updateDto
    });
    this.closeAllModals();
  }

  /**
   * Create new Apprise provider
   */
  private createAppriseProvider(data: AppriseFormData): void {
    const createDto: CreateNotificationProviderDto = {
      name: data.name,
      type: NotificationProviderType.Apprise,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      configuration: {
        url: data.fullUrl,
        key: data.key,
        tags: data.tags
      }
    };

    this.notificationProviderStore.createProvider(createDto);
    this.closeAllModals();
  }

  /**
   * Update existing Apprise provider
   */
  private updateAppriseProvider(data: AppriseFormData): void {
    if (!this.editingProvider) return;

    const updateDto: UpdateNotificationProviderDto = {
      name: data.name,
      type: this.editingProvider.type,
      isEnabled: data.enabled,
      onFailedImportStrike: data.onFailedImportStrike,
      onStalledStrike: data.onStalledStrike,
      onSlowStrike: data.onSlowStrike,
      onQueueItemDeleted: data.onQueueItemDeleted,
      onDownloadCleaned: data.onDownloadCleaned,
      onCategoryChanged: data.onCategoryChanged,
      configuration: {
        url: data.fullUrl,
        key: data.key,
        tags: data.tags
      }
    };

    this.notificationProviderStore.updateProvider({ 
      id: this.editingProvider.id, 
      provider: updateDto
    });
    this.closeAllModals();
  }
} 
