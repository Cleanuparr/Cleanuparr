import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { GeneralConfigStore } from "./general-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { GeneralConfig } from "../../shared/models/general-config.model";
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
import { AutoCompleteModule } from "primeng/autocomplete";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { ConfirmationService } from "primeng/api";
import { MobileAutocompleteComponent } from "../../shared/components/mobile-autocomplete/mobile-autocomplete.component";

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
    AutoCompleteModule,
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
  readonly generalError = this.generalConfigStore.error;

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
      searchDelay: [30, [Validators.required, Validators.min(1), Validators.max(300)]],
      logLevel: [LogEventLevel.Information],
      ignoredDownloads: [[]],
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
          logLevel: config.logLevel,
          ignoredDownloads: config.ignoredDownloads || [],
        });

        // Store original values for dirty checking
        this.storeOriginalValues();

        // Track the support banner state for confirmation dialog logic
        this.previousSupportBannerState = config.displaySupportBanner;
        
        // Mark form as initialized to enable confirmation dialogs for user actions
        this.formInitialized = true;

        // Mark form as pristine since we've just loaded the data
        this.generalForm.markAsPristine();
      }
    });

    // Effect to handle errors
    effect(() => {
      const errorMessage = this.generalError();
      if (errorMessage) {
        // Only emit the error for parent components
        this.error.emit(errorMessage);
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
    // Mark all form controls as touched to trigger validation
    this.markFormGroupTouched(this.generalForm);

    if (this.generalForm.valid) {
      const formValues = this.generalForm.getRawValue();

      const config: GeneralConfig = {
        displaySupportBanner: formValues.displaySupportBanner,
        dryRun: formValues.dryRun,
        httpMaxRetries: formValues.httpMaxRetries,
        httpTimeout: formValues.httpTimeout,
        httpCertificateValidation: formValues.httpCertificateValidation,
        searchEnabled: formValues.searchEnabled,
        searchDelay: formValues.searchDelay,
        logLevel: formValues.logLevel,
        ignoredDownloads: formValues.ignoredDownloads || [],
      };

      // Save the configuration
      this.generalConfigStore.saveConfig(config);
      
      // Setup a one-time check to mark form as pristine after successful save
      const checkSaveCompletion = () => {
        const saving = this.generalSaving();
        const error = this.generalError();
        
        if (!saving && !error) {
          // Mark form as pristine after successful save
          this.generalForm.markAsPristine();
          // Update original values reference
          this.storeOriginalValues();
          // Emit saved event 
          this.saved.emit();
          // Display success message
          this.notificationService.showSuccess('General configuration saved successfully.');
        } else if (!saving && error) {
          // If there's an error, we can stop checking
          // No need to show error toast here, it's handled by the LoadingErrorStateComponent
        } else {
          // If still saving, check again in a moment
          setTimeout(checkSaveCompletion, 100);
        }
      };
      
      // Start checking for save completion
      checkSaveCompletion();
    } else {
      this.notificationService.showValidationError();
    }
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
      searchDelay: 30,
      logLevel: LogEventLevel.Information,
      ignoredDownloads: [],
    });
    
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
    return control ? control.dirty && control.hasError(errorName) : false;
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
