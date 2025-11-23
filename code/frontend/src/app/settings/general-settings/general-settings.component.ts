import { Component, EventEmitter, OnInit, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { GeneralConfigStore } from "./general-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { GeneralConfig } from "../../shared/models/general-config.model";
import { LoggingConfig } from "../../shared/models/logging-config.model";
import { LogEventLevel } from "../../shared/models/log-event-level.enum";
import { CertificateValidationType } from "../../shared/models/certificate-validation-type.enum";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { ToastModule } from "primeng/toast";
import { NotificationService } from '../../core/services/notification.service';
import { DocumentationService } from '../../core/services/documentation.service';
import { SelectModule } from "primeng/select";
import { ChipsModule } from "primeng/chips";
import { ChipModule } from "primeng/chip";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { ConfirmationService } from "primeng/api";
import { MobileAutocompleteComponent } from "../../shared/components/mobile-autocomplete/mobile-autocomplete.component";
import { ErrorHandlerUtil } from "../../core/utils/error-handler.util";

@Component({
  selector: "app-general-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    InputNumberModule,
    ChipsModule,
    ChipModule,
    ToastModule,
    SelectModule,
    LoadingErrorStateComponent,
    ConfirmDialogModule,
    MobileAutocompleteComponent,
  ],
  providers: [GeneralConfigStore, ConfirmationService],
  templateUrl: "./general-settings.component.html",
  styleUrls: ["./general-settings.component.scss"],
})
export class GeneralSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // General Configuration Form
  generalForm: FormGroup;
  
  // Getter for easy access to the log form group
  get logForm(): FormGroup {
    return this.generalForm.get('log') as FormGroup;
  }
  
  // Original form values for tracking changes
  private originalFormValues: any;
  
  // Track whether the form has actual changes compared to original values
  hasActualChanges = false;
  
  // Log level options for dropdown
  logLevelOptions = [
    { label: "Verbose", value: LogEventLevel.Verbose },
    { label: "Debug", value: LogEventLevel.Debug },
    { label: "Information", value: LogEventLevel.Information },
    { label: "Warning", value: LogEventLevel.Warning },
    { label: "Error", value: LogEventLevel.Error },
    { label: "Fatal", value: LogEventLevel.Fatal },
  ];
  
  // Certificate validation options for dropdown
  certificateValidationOptions = [
    { label: "Enabled", value: CertificateValidationType.Enabled },
    { label: "Disabled for Local Addresses", value: CertificateValidationType.DisabledForLocalAddresses },
    { label: "Disabled", value: CertificateValidationType.Disabled },
  ];

  // Inject the necessary services
  private formBuilder = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private documentationService = inject(DocumentationService);
  private generalConfigStore = inject(GeneralConfigStore);
  private confirmationService = inject(ConfirmationService);

  // Signals from the store
  readonly generalConfig = this.generalConfigStore.config;
  readonly generalLoading = this.generalConfigStore.loading;
  readonly generalSaving = this.generalConfigStore.saving;
  readonly generalLoadError = this.generalConfigStore.loadError;  // Only for "Not connected" state
  readonly generalSaveError = this.generalConfigStore.saveError;  // Only for toast notifications

  // Subject for unsubscribing from observables when component is destroyed
  private destroy$ = new Subject<void>();
  
  // Track the previous support banner state to detect when user is trying to disable
  private previousSupportBannerState = true;
  
  // Flag to track if form has been initially loaded to avoid showing dialog on page load
  private formInitialized = false;

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return !this.generalForm.dirty;
  }

  /**
   * Open field-specific documentation in a new tab
   * @param fieldName The form field name (e.g., 'dryRun', 'httpMaxRetries')
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('general', fieldName);
  }

  constructor() {
    // Initialize the general settings form
    this.generalForm = this.formBuilder.group({
      displaySupportBanner: [true],
      dryRun: [false],
      httpMaxRetries: [0, [Validators.required,Validators.min(0), Validators.max(5)]],
      httpTimeout: [100, [Validators.required, Validators.min(1), Validators.max(100)]],
      httpCertificateValidation: [CertificateValidationType.Enabled],
      searchEnabled: [true],
      searchDelay: [120, [Validators.required, Validators.min(60), Validators.max(300)]],
      ignoredDownloads: [[]],
      log: this.formBuilder.group({
        level: [LogEventLevel.Information],
        rollingSizeMB: [10, [Validators.required, Validators.min(0), Validators.max(100)]],
        retainedFileCount: [5, [Validators.required, Validators.min(0), Validators.max(50)]],
        timeLimitHours: [24, [Validators.required, Validators.min(0), Validators.max(1440)]], // max 60 days
        archiveEnabled: [true],
        archiveRetainedCount: [{ value: 60, disabled: false }, [Validators.required, Validators.min(0), Validators.max(100)]],
        archiveTimeLimitHours: [{ value: 720, disabled: false }, [Validators.required, Validators.min(0), Validators.max(1440)]], // max 60 days
      }),
    });

    // Effect to handle configuration changes
    effect(() => {
      const config = this.generalConfig();
      if (config) {
        // Reset form with the config values
        this.generalForm.patchValue({
          displaySupportBanner: config.displaySupportBanner,
          dryRun: config.dryRun,
          httpMaxRetries: config.httpMaxRetries,
          httpTimeout: config.httpTimeout,
          httpCertificateValidation: config.httpCertificateValidation,
          searchEnabled: config.searchEnabled,
          searchDelay: config.searchDelay,
          ignoredDownloads: config.ignoredDownloads || [],
          log: config.log || {
            level: LogEventLevel.Information,
            rollingSizeMB: 10,
            retainedFileCount: 5,
            timeLimitHours: 24,
            archiveEnabled: true,
            archiveRetainedCount: 60,
            archiveTimeLimitHours: 720,
          },
        });

        // Store original values for dirty checking
        this.storeOriginalValues();

        // Update archive controls state based on loaded configuration
        const archiveEnabled = config.log?.archiveEnabled ?? true;
        this.updateArchiveControlsState(archiveEnabled);

        // Track the support banner state for confirmation dialog logic
        this.previousSupportBannerState = config.displaySupportBanner;
        
        // Mark form as initialized to enable confirmation dialogs for user actions
        this.formInitialized = true;

        // Mark form as pristine since we've just loaded the data
        this.generalForm.markAsPristine();
      }
    });

    // Effect to handle load errors - emit to LoadingErrorStateComponent for "Not connected" display
    effect(() => {
      const loadErrorMessage = this.generalLoadError();
      if (loadErrorMessage) {
        // Load errors should be shown as "Not connected to server" in LoadingErrorStateComponent
        this.error.emit(loadErrorMessage);
      }
    });
    
    // Effect to handle save errors - show as toast notifications for user to fix
    effect(() => {
      const saveErrorMessage = this.generalSaveError();
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
    // Listen for changes to the 'displaySupportBanner' control
    const supportBannerControl = this.generalForm.get('displaySupportBanner');
    if (supportBannerControl) {
      supportBannerControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(enabled => {
          // Only show confirmation dialog if form is initialized and user is trying to disable
          if (this.formInitialized && !enabled && this.previousSupportBannerState) {
            this.showDisableSupportBannerConfirmationDialog();
          } else {
            // Update state tracking
            this.previousSupportBannerState = enabled;
          }
        });
    }
    
    // Listen to all form changes to check for actual differences from original values
    this.generalForm.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasActualChanges = this.formValuesChanged();
      });
    
    // Listen for changes to the 'archiveEnabled' control
    const archiveEnabledControl = this.generalForm.get('log.archiveEnabled');
    if (archiveEnabledControl) {
      archiveEnabledControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe((enabled: boolean) => {
          this.updateArchiveControlsState(enabled);
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
    
    const currentValues = this.generalForm.getRawValue();
    return !this.isEqual(currentValues, this.originalFormValues);
  }

  /**
   * Update the state of archive-related controls based on the 'archiveEnabled' control value
   */
  private updateArchiveControlsState(archiveEnabled: boolean): void {
    const archiveRetainedCountControl = this.generalForm.get('log.archiveRetainedCount');
    const archiveTimeLimitHoursControl = this.generalForm.get('log.archiveTimeLimitHours');

    if (archiveEnabled) {
      archiveRetainedCountControl?.enable({ emitEvent: false });
      archiveTimeLimitHoursControl?.enable({ emitEvent: false });
    } else {
      // Disable controls but ensure they can still show validation errors
      archiveRetainedCountControl?.disable({ emitEvent: false });
      archiveTimeLimitHoursControl?.disable({ emitEvent: false });
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
   * Validate archive controls specifically, even when disabled
   * Returns true if archive controls have validation errors
   */
  private validateArchiveControls(): boolean {
    const archiveEnabledControl = this.generalForm.get('log.archiveEnabled');
    const archiveRetainedCountControl = this.generalForm.get('log.archiveRetainedCount');
    const archiveTimeLimitHoursControl = this.generalForm.get('log.archiveTimeLimitHours');
    
    if (!archiveEnabledControl || !archiveRetainedCountControl || !archiveTimeLimitHoursControl) {
      return false;
    }
    
    const isArchiveEnabled = archiveEnabledControl.value;
    
    // If archive is disabled, we need to manually validate the disabled controls
    if (!isArchiveEnabled) {
      const retainedCountValue = archiveRetainedCountControl.value;
      const timeLimitValue = archiveTimeLimitHoursControl.value;
      
      // Check archive retained count validation (required, min: 0, max: 100)
      const retainedCountErrors: any = {};
      if (retainedCountValue === null || retainedCountValue === undefined || retainedCountValue === '') {
        retainedCountErrors.required = true;
      } else if (retainedCountValue < 0) {
        retainedCountErrors.min = { min: 0, actual: retainedCountValue };
      } else if (retainedCountValue > 100) {
        retainedCountErrors.max = { max: 100, actual: retainedCountValue };
      }
      
      // Check archive time limit validation (required, min: 0, max: 1440)
      const timeLimitErrors: any = {};
      if (timeLimitValue === null || timeLimitValue === undefined || timeLimitValue === '') {
        timeLimitErrors.required = true;
      } else if (timeLimitValue < 0) {
        timeLimitErrors.min = { min: 0, actual: timeLimitValue };
      } else if (timeLimitValue > 1440) {
        timeLimitErrors.max = { max: 1440, actual: timeLimitValue };
      }
      
      // Manually set errors and mark as touched to show validation messages
      if (Object.keys(retainedCountErrors).length > 0) {
        archiveRetainedCountControl.setErrors(retainedCountErrors);
        archiveRetainedCountControl.markAsTouched();
      } else {
        archiveRetainedCountControl.setErrors(null);
      }
      
      if (Object.keys(timeLimitErrors).length > 0) {
        archiveTimeLimitHoursControl.setErrors(timeLimitErrors);
        archiveTimeLimitHoursControl.markAsTouched();
      } else {
        archiveTimeLimitHoursControl.setErrors(null);
      }
      
      // Return true if there are validation errors
      return Object.keys(retainedCountErrors).length > 0 || Object.keys(timeLimitErrors).length > 0;
    }
    
    return false;
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
    this.originalFormValues = JSON.parse(JSON.stringify(this.generalForm.getRawValue()));
    this.hasActualChanges = false;
  }

  /**
   * Save the general configuration
   */
  saveGeneralConfig(): void {
    // Force validation on all controls, including disabled ones
    this.validateAllFormControls(this.generalForm);
    
    // Specifically validate archive controls even when disabled
    const archiveValidationErrors = this.validateArchiveControls();
    
    // Mark all form controls as touched to trigger validation messages
    this.markFormGroupTouched(this.generalForm);

    if (this.generalForm.invalid || archiveValidationErrors) {
      this.notificationService.showValidationError();
      return;
    }

    const formValues = this.generalForm.getRawValue();

    const config: GeneralConfig = {
      displaySupportBanner: formValues.displaySupportBanner,
      dryRun: formValues.dryRun,
      httpMaxRetries: formValues.httpMaxRetries,
      httpTimeout: formValues.httpTimeout,
      httpCertificateValidation: formValues.httpCertificateValidation,
      searchEnabled: formValues.searchEnabled,
      searchDelay: formValues.searchDelay,
      ignoredDownloads: formValues.ignoredDownloads || [],
      log: formValues.log as LoggingConfig,
    };

      // Save the configuration
      this.generalConfigStore.saveConfig(config);
      
      // Setup a one-time check to mark form as pristine after successful save
      const checkSaveCompletion = () => {
        const saving = this.generalSaving();
        const saveError = this.generalSaveError();
        
        if (!saving && !saveError) {
          // Mark form as pristine after successful save
          this.generalForm.markAsPristine();
          // Update original values reference
          this.storeOriginalValues();
          // Emit saved event 
          this.saved.emit();
          // Display success message
          this.notificationService.showSuccess('General configuration saved successfully.');
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
   * Reset the general configuration form to default values
   */
  resetGeneralConfig(): void {  
    this.generalForm.reset({
      displaySupportBanner: true,
      dryRun: false,
      httpMaxRetries: 0,
      httpTimeout: 100,
      httpCertificateValidation: CertificateValidationType.Enabled,
      searchEnabled: true,
      searchDelay: 120,
      ignoredDownloads: [],
      log: {
        level: LogEventLevel.Information,
        rollingSizeMB: 10,
        retainedFileCount: 5,
        timeLimitHours: 24,
        archiveEnabled: true,
        archiveRetainedCount: 60,
        archiveTimeLimitHours: 720,
      },
    });
    
    // Update archive controls state after reset
    this.updateArchiveControlsState(true); // archiveEnabled defaults to true
    
    // Mark form as dirty so the save button is enabled after reset
    this.generalForm.markAsDirty();
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
    const control = this.generalForm.get(controlName);
    // Check for errors on both enabled and disabled controls that have been touched
    return control ? control.hasError(errorName) : false;
  }

  /**
   * Get nested form control errors
   */
  hasNestedError(parentName: string, controlName: string, errorName: string): boolean {
    const parentControl = this.generalForm.get(parentName);
    if (!parentControl || !(parentControl instanceof FormGroup)) {
      return false;
    }

    const control = parentControl.get(controlName);
    // Check for errors on both enabled and disabled controls that have been touched
    return control ? control.hasError(errorName) : false;
  }

  /**
   * Show disable support banner confirmation dialog
   */
  private showDisableSupportBannerConfirmationDialog(): void {
    this.confirmationService.confirm({
      header: 'Support Cleanuparr',
      message: `
        <div style="text-align: left; line-height: 1.6;">
          <p style="margin-bottom: 15px; color: #60a5fa; font-weight: 500;">
            If you haven't already, please consider giving us a <i class="pi pi-star"></i> on 
            <a href="https://github.com/Cleanuparr/Cleanuparr" target="_blank" style="color: #60a5fa; text-decoration: underline;">GitHub</a> 
            to help spread the word!
          </p>
          <p style="margin-bottom: 20px; font-style: italic; font-size: 14px; color: #9ca3af;">
            Thank you for using Cleanuparr and for your support! <i class="pi pi-heart"></i>
          </p>
        </div>
      `,
      icon: 'pi pi-heart',
      acceptIcon: 'pi pi-check',
      acceptLabel: 'OK',
      rejectVisible: false,
      accept: () => {
        // User acknowledged the message, update state tracking to allow disabling
        this.previousSupportBannerState = false;
      }
    });
  }
}
