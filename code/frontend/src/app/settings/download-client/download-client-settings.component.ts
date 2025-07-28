import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { DownloadClientConfigStore } from "./download-client-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { ClientConfig, DownloadClientConfig, CreateDownloadClientDto } from "../../shared/models/download-client-config.model";
import { DownloadClientType, DownloadClientTypeName } from "../../shared/models/enums";
import { DocumentationService } from "../../core/services/documentation.service";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { SelectModule } from 'primeng/select';
import { ToastModule } from "primeng/toast";
import { DialogModule } from "primeng/dialog";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { ConfirmationService } from "primeng/api";
import { NotificationService } from "../../core/services/notification.service";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";

@Component({
  selector: "app-download-client-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    InputNumberModule,
    SelectModule,
    ToastModule,
    DialogModule,
    ConfirmDialogModule,
    LoadingErrorStateComponent
  ],
  providers: [DownloadClientConfigStore, ConfirmationService],
  templateUrl: "./download-client-settings.component.html",
  styleUrls: ["./download-client-settings.component.scss"],
})
export class DownloadClientSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Forms
  clientForm: FormGroup;

  // Modal state
  showClientModal = false;
  modalMode: 'add' | 'edit' = 'add';
  editingClient: ClientConfig | null = null;

  // Download client type options
  typeNameOptions: { label: string, value: DownloadClientTypeName }[] = [];

  // Clean up subscriptions
  private destroy$ = new Subject<void>();

  // Services
  private formBuilder = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private downloadClientStore = inject(DownloadClientConfigStore);
  private documentationService = inject(DocumentationService);

  // Signals from store
  downloadClientConfig = this.downloadClientStore.config;
  downloadClientLoading = this.downloadClientStore.loading;
  downloadClientError = this.downloadClientStore.error;
  downloadClientSaving = this.downloadClientStore.saving;

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return true; // No unsaved changes in modal-based approach
  }

  constructor() {
    // Initialize client form for modal
    this.clientForm = this.formBuilder.group({
      name: ['', Validators.required],
      typeName: [null, Validators.required],
      host: ['', [Validators.required, this.uriValidator.bind(this)]],
      username: [''],
      password: [''],
      urlBase: [''],
      enabled: [true]
    });

    // Initialize type name options
    for (const key of Object.keys(DownloadClientTypeName)) {
      this.typeNameOptions.push({ 
        label: key, 
        value: DownloadClientTypeName[key as keyof typeof DownloadClientTypeName] 
      });
    }

    // Load Download Client config data
    this.downloadClientStore.loadConfig();

    // Setup client type change handler
    this.clientForm.get('typeName')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.onClientTypeChange();
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
   * Custom validator to check if the input is a valid URI
   */
  private uriValidator(control: AbstractControl): ValidationErrors | null {
    if (!control.value) {
      return null; // Let required validator handle empty values
    }
    
    try {
      const url = new URL(control.value);
      
      // Check that we have a valid protocol (http or https)
      if (url.protocol !== 'http:' && url.protocol !== 'https:') {
        return { invalidProtocol: true };
      }
      
      return null; // Valid URI
    } catch (e) {
      return { invalidUri: true }; // Invalid URI
    }
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
   * Get clients from current config
   */
  get clients(): ClientConfig[] {
    return this.downloadClientConfig()?.clients || [];
  }

  /**
   * Open modal to add new client
   */
  openAddClientModal(): void {
    this.modalMode = 'add';
    this.editingClient = null;
    this.clientForm.reset();
    this.clientForm.patchValue({ enabled: true }); // Default enabled to true
    this.showClientModal = true;
  }

  /**
   * Open modal to edit existing client
   */
  openEditClientModal(client: ClientConfig): void {
    this.modalMode = 'edit';
    this.editingClient = client;
    
    this.clientForm.patchValue({
      name: client.name,
      typeName: client.typeName,
      host: client.host,
      username: client.username,
      password: client.password,
      urlBase: client.urlBase,
      enabled: client.enabled
    });
    this.showClientModal = true;
  }

  /**
   * Close client modal
   */
  closeClientModal(): void {
    this.showClientModal = false;
    this.editingClient = null;
    this.clientForm.reset();
  }

  /**
   * Save client (add or edit)
   */
  saveClient(): void {
    this.markFormGroupTouched(this.clientForm);

    if (this.clientForm.invalid) {
      this.notificationService.showError('Please fix the validation errors before saving');
      return;
    }

    const formValue = this.clientForm.value;

    if (this.modalMode === 'add') {
      const clientData: CreateDownloadClientDto = {
        name: formValue.name,
        type: this.mapTypeNameToType(formValue.typeName),
        typeName: formValue.typeName,
        host: formValue.host,
        username: formValue.username,
        password: formValue.password,
        urlBase: formValue.urlBase,
        enabled: formValue.enabled
      };
      
      this.downloadClientStore.createClient(clientData);
    } else if (this.editingClient) {
      // For updates, create a proper ClientConfig object
      const clientConfig: ClientConfig = {
        id: this.editingClient.id,
        name: formValue.name,
        type: this.mapTypeNameToType(formValue.typeName),
        typeName: formValue.typeName,
        host: formValue.host,
        username: formValue.username,
        password: formValue.password,
        urlBase: formValue.urlBase,
        enabled: formValue.enabled
      };
      
      this.downloadClientStore.updateClient({ 
        id: this.editingClient.id!, 
        client: clientConfig
      });
    }

    this.monitorClientSaving();
  }

  /**
   * Monitor client saving completion
   */
  private monitorClientSaving(): void {
    const checkSavingStatus = () => {
      const saving = this.downloadClientSaving();
      const error = this.downloadClientError();
      
      if (!saving) {
        if (error) {
          this.notificationService.showError(`Operation failed: ${error}`);
        } else {
          const action = this.modalMode === 'add' ? 'created' : 'updated';
          this.notificationService.showSuccess(`Client ${action} successfully`);
          this.closeClientModal();
        }
      } else {
        setTimeout(checkSavingStatus, 100);
      }
    };
    
    setTimeout(checkSavingStatus, 100);
  }

  /**
   * Delete client with confirmation
   */
  deleteClient(client: ClientConfig): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to delete the client "${client.name}"?`,
      header: 'Confirm Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.downloadClientStore.deleteClient(client.id!);
        
        // Monitor deletion
        const checkDeletionStatus = () => {
          const saving = this.downloadClientSaving();
          const error = this.downloadClientError();
          
          if (!saving) {
            if (error) {
              this.notificationService.showError(`Deletion failed: ${error}`);
            } else {
              this.notificationService.showSuccess('Client deleted successfully');
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
   * Get modal title based on mode
   */
  get modalTitle(): string {
    return this.modalMode === 'add' ? 'Add Download Client' : 'Edit Download Client';
  }

  /**
   * Map typeName to type category
   */
  private mapTypeNameToType(typeName: DownloadClientTypeName): DownloadClientType {
    switch (typeName) {
      case DownloadClientTypeName.qBittorrent:
      case DownloadClientTypeName.Deluge:
      case DownloadClientTypeName.Transmission:
      case DownloadClientTypeName.uTorrent:
        return DownloadClientType.Torrent;
      default:
        throw new Error(`Unknown client type name: ${typeName}`);
    }
  }
  
  /**
   * Handle client type changes to update validation
   */
  onClientTypeChange(): void {
    const clientTypeName = this.clientForm.get('typeName')?.value;
    const hostControl = this.clientForm.get('host');
    const usernameControl = this.clientForm.get('username');
    const urlBaseControl = this.clientForm.get('urlBase');
    
    if (!hostControl || !usernameControl || !urlBaseControl) return;
    
    hostControl.setValidators([
      Validators.required, 
      this.uriValidator.bind(this)
    ]);
    
    // Clear username value and remove validation for Deluge
    if (clientTypeName === DownloadClientTypeName.Deluge) {
      usernameControl.setValue('');
      usernameControl.clearValidators();
    }
    
    // Set default URL base for Transmission
    if (clientTypeName === DownloadClientTypeName.Transmission) {
      urlBaseControl.setValue('transmission');
    }
    
    // Update validation state
    hostControl.updateValueAndValidity();
    usernameControl.updateValueAndValidity();
  }

  /**
   * Check if username field should be shown (hidden for Deluge)
   */
  shouldShowUsernameField(): boolean {
    const clientTypeName = this.clientForm.get('typeName')?.value;
    return clientTypeName !== DownloadClientTypeName.Deluge;
  }

  /**
   * Get client type label for display
   */
  getClientTypeLabel(client: ClientConfig): string {
    const option = this.typeNameOptions.find(opt => opt.value === client.typeName);
    return option?.label || 'Unknown';
  }

  /**
   * Open field-specific documentation
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('download-client', fieldName);
  }
}
