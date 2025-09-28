import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { BlacklistSyncConfigStore } from "./blacklist-sync-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { BlacklistSyncConfig } from "../../shared/models/blacklist-sync-config.model";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { ToastModule } from "primeng/toast";
import { NotificationService } from '../../core/services/notification.service';
import { DocumentationService } from '../../core/services/documentation.service';
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";

@Component({
  selector: "app-blacklist-sync-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    ToastModule,
    LoadingErrorStateComponent,
  ],
  providers: [BlacklistSyncConfigStore],
  templateUrl: "./blacklist-sync-settings.component.html",
  styleUrls: ["./blacklist-sync-settings.component.scss"],
})
export class BlacklistSyncSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Blacklist Sync Configuration Form
  blacklistSyncForm: FormGroup;
  
  // Original form values for tracking changes
  private originalFormValues: any;
  
  // Track whether the form has actual changes compared to original values
  hasActualChanges = false;

  // Inject the necessary services
  private formBuilder = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private documentationService = inject(DocumentationService);
  private blacklistSyncConfigStore = inject(BlacklistSyncConfigStore);

  // Signals from the store
  readonly blacklistSyncConfig = this.blacklistSyncConfigStore.config;
  readonly blacklistSyncLoading = this.blacklistSyncConfigStore.loading;
  readonly blacklistSyncSaving = this.blacklistSyncConfigStore.saving;
  readonly blacklistSyncLoadError = this.blacklistSyncConfigStore.loadError;  // Only for "Not connected" state
  readonly blacklistSyncSaveError = this.blacklistSyncConfigStore.saveError;  // Only for toast notifications

  // Subject for unsubscribing from observables when component is destroyed
  private destroy$ = new Subject<void>();
  
  // Flag to track if form has been initially loaded to avoid showing dialog on page load
  private formInitialized = false;

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return !this.blacklistSyncForm.dirty;
  }

  /**
   * Open field-specific documentation in a new tab
   * @param fieldName The form field name (e.g., 'enabled', 'blacklistPath')
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('blacklist-sync', fieldName);
  }

  constructor() {
    // Initialize the blacklist sync settings form
    this.blacklistSyncForm = this.formBuilder.group({
      enabled: [false],
      blacklistPath: ['', [Validators.required]],
    });

    // Effect to handle configuration changes
    effect(() => {
      const config = this.blacklistSyncConfig();
      if (config) {
        // Reset form with the config values
        this.blacklistSyncForm.patchValue({
          enabled: config.enabled,
          blacklistPath: config.blacklistPath || '',
        });

        // Store original values for dirty checking
        this.storeOriginalValues();

        // Update blacklist path controls state based on loaded configuration
        const blacklistSyncEnabled = config.enabled ?? false;
        this.updateBlacklistPathControlState(blacklistSyncEnabled);
        
        // Mark form as initialized to enable confirmation dialogs for user actions
        this.formInitialized = true;

        // Mark form as pristine since we've just loaded the data
        this.blacklistSyncForm.markAsPristine();
      }
    });

    // Effect to handle load errors - emit to LoadingErrorStateComponent for "Not connected" display
    effect(() => {
      const loadErrorMessage = this.blacklistSyncLoadError();
      if (loadErrorMessage) {
        // Load errors should be shown as "Not connected to server" in LoadingErrorStateComponent
        this.error.emit(loadErrorMessage);
      }
    });
    
    // Effect to handle save errors - show as toast notifications for user to fix
    effect(() => {
      const saveErrorMessage = this.blacklistSyncSaveError();
      if (saveErrorMessage) {
          // Always show save errors as a toast so the user sees the backend message.
          this.notificationService.showError(saveErrorMessage);
      }
    });

    // Set up listeners for form value changes
    this.setupFormValueChangeListeners();
  }

  /**
   * Set up listeners for form control value changes
   */
  private setupFormValueChangeListeners(): void {
    // Listen to all form changes to check for actual differences from original values
    this.blacklistSyncForm.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasActualChanges = this.formValuesChanged();
      });

    // Listen for changes to the 'enabled' control
    const enabledControl = this.blacklistSyncForm.get('enabled');
    if (enabledControl) {
      enabledControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe((enabled: boolean) => {
          this.updateBlacklistPathControlState(enabled);
        });
    }
  }

  /**
   * Clean up subscriptions when component is destroyed
   */
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Check if the current form values are different from the original values
   */
  private formValuesChanged(): boolean {
    if (!this.originalFormValues) return false;
    
    const currentValues = this.blacklistSyncForm.getRawValue();
    return !this.isEqual(currentValues, this.originalFormValues);
  }

  /**
   * Update blacklist path control state based on enabled value
   */
  private updateBlacklistPathControlState(enabled: boolean): void {
    const blacklistPathControl = this.blacklistSyncForm.get('blacklistPath');

    if (enabled) {
      blacklistPathControl?.enable({ emitEvent: false });
    } else {
      blacklistPathControl?.disable({ emitEvent: false });
    }
  }

  /**
   * Validate all form controls, including disabled ones
   */
  private validateAllFormControls(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(key => {
      const control = formGroup.get(key);
      if (control instanceof FormGroup) {
        this.validateAllFormControls(control);
      } else {
        // Force validation even on disabled controls
        control?.updateValueAndValidity({ onlySelf: true });
        control?.markAsTouched();
      }
    });
  }

  /**
   * Deep compare two objects for equality
   */
  private isEqual(obj1: any, obj2: any): boolean {
    if (obj1 === obj2) return true;
    
    if (typeof obj1 !== 'object' || obj1 === null ||
        typeof obj2 !== 'object' || obj2 === null) {
      return obj1 === obj2;
    }
    
    if (Array.isArray(obj1) && Array.isArray(obj2)) {
      if (obj1.length !== obj2.length) return false;
      for (let i = 0; i < obj1.length; i++) {
        if (!this.isEqual(obj1[i], obj2[i])) return false;
      }
      return true;
    }
    
    const keys1 = Object.keys(obj1);
    const keys2 = Object.keys(obj2);
    
    if (keys1.length !== keys2.length) return false;
    
    for (const key of keys1) {
      if (!keys2.includes(key)) return false;
      
      if (!this.isEqual(obj1[key], obj2[key])) return false;
    }
    
    return true;
  }

  /**
   * Store original form values for dirty checking
   */
  private storeOriginalValues(): void {
    // Create a deep copy of the form values to ensure proper comparison
    this.originalFormValues = JSON.parse(JSON.stringify(this.blacklistSyncForm.getRawValue()));
    this.hasActualChanges = false;
  }

  /**
   * Save the blacklist sync configuration
   */
  saveBlacklistSyncConfig(): void {
    // Force validation on all controls, including disabled ones
    this.validateAllFormControls(this.blacklistSyncForm);
    
    // Mark all form controls as touched to trigger validation messages
    this.markFormGroupTouched(this.blacklistSyncForm);

    if (this.blacklistSyncForm.invalid) {
      this.notificationService.showValidationError();
      return;
    }

    const formValues = this.blacklistSyncForm.getRawValue();

    const config: BlacklistSyncConfig = {
      id: this.blacklistSyncConfig()?.id || '',
      enabled: formValues.enabled,
      blacklistPath: formValues.blacklistPath || undefined,
    };

      // Save the configuration
      this.blacklistSyncConfigStore.saveConfig(config);
      
      // Setup a one-time check to mark form as pristine after successful save
      const checkSaveCompletion = () => {
        const saving = this.blacklistSyncSaving();
        const saveError = this.blacklistSyncSaveError();
        
        if (!saving && !saveError) {
          // Mark form as pristine after successful save
          this.blacklistSyncForm.markAsPristine();
          // Update original values reference
          this.storeOriginalValues();
          // Emit saved event 
          this.saved.emit();
          // Display success message
          this.notificationService.showSuccess('Blacklist Sync configuration saved successfully.');
        } else if (!saving && saveError) {
          // If there's a save error, we can stop checking
          // Toast notification is already handled by the effect above
        } else {
          // If still saving, check again in a moment
          setTimeout(checkSaveCompletion, 100);
        }
      };
      
      // Start checking for save completion
      checkSaveCompletion();
  }

  /**
   * Reset the blacklist sync configuration form to default values
   */
  resetBlacklistSyncConfig(): void {  
    this.blacklistSyncForm.reset({
      enabled: false,
      blacklistPath: '',
    });
    
    // Update blacklist path control state after reset
    this.updateBlacklistPathControlState(false); // enabled defaults to false
    
    // Mark form as dirty so the save button is enabled after reset
    this.blacklistSyncForm.markAsDirty();
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
   * Check if a form control has an error after it's been touched
   */
  hasError(controlName: string, errorName: string): boolean {
    const control = this.blacklistSyncForm.get(controlName);
    // Check for errors on both enabled and disabled controls that have been touched
    return control ? control.hasError(errorName) : false;
  }
}
