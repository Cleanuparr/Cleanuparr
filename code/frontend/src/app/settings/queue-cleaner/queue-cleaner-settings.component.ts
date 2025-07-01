import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { QueueCleanerConfigStore } from "./queue-cleaner-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import {
  QueueCleanerConfig,
  ScheduleUnit,
  FailedImportConfig,
  StalledConfig,
  SlowConfig,
  ScheduleOptions
} from "../../shared/models/queue-cleaner-config.model";
import { SettingsCardComponent } from "../components/settings-card/settings-card.component";
import { ByteSizeInputComponent } from "../../shared/components/byte-size-input/byte-size-input.component";
import { MobileAutocompleteComponent } from "../../shared/components/mobile-autocomplete/mobile-autocomplete.component";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { AccordionModule } from "primeng/accordion";
import { SelectButtonModule } from "primeng/selectbutton";
import { ChipsModule } from "primeng/chips";
import { ToastModule } from "primeng/toast";
// Using centralized NotificationService instead of MessageService
import { NotificationService } from "../../core/services/notification.service";
import { DocumentationService } from "../../core/services/documentation.service";
import { SelectModule } from "primeng/select";
import { AutoCompleteModule } from "primeng/autocomplete";
import { DropdownModule } from "primeng/dropdown";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";
import { ErrorHandlerUtil } from "../../core/utils/error-handler.util";

@Component({
  selector: "app-queue-cleaner-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    InputNumberModule,
    AccordionModule,
    SelectButtonModule,
    ChipsModule,
    ToastModule,
    ByteSizeInputComponent,
    SelectModule,
    AutoCompleteModule,
    DropdownModule,
    LoadingErrorStateComponent,
    MobileAutocompleteComponent,
  ],
  providers: [QueueCleanerConfigStore],
  templateUrl: "./queue-cleaner-settings.component.html",
  styleUrls: ["./queue-cleaner-settings.component.scss"],
})
export class QueueCleanerSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Queue Cleaner Configuration Form
  queueCleanerForm: FormGroup;
  
  // Original form values for tracking changes
  private originalFormValues: any;
  
  // Track whether the form has actual changes compared to original values
  hasActualChanges = false;

  // Schedule unit options for job schedules
  scheduleUnitOptions = [
    { label: "Seconds", value: ScheduleUnit.Seconds },
    { label: "Minutes", value: ScheduleUnit.Minutes },
    { label: "Hours", value: ScheduleUnit.Hours },
  ];
  
  // Options for each schedule unit
  scheduleValueOptions = {
    [ScheduleUnit.Seconds]: ScheduleOptions[ScheduleUnit.Seconds].map(v => ({ label: v.toString(), value: v })),
    [ScheduleUnit.Minutes]: ScheduleOptions[ScheduleUnit.Minutes].map(v => ({ label: v.toString(), value: v })),
    [ScheduleUnit.Hours]: ScheduleOptions[ScheduleUnit.Hours].map(v => ({ label: v.toString(), value: v }))
  };
  
  // Display modes for schedule
  scheduleModeOptions = [
    { label: 'Basic', value: false },
    { label: 'Advanced', value: true }
  ];

  // Inject the necessary services
  private formBuilder = inject(FormBuilder);
  // Using the notification service for all toast messages
  private notificationService = inject(NotificationService);
  private documentationService = inject(DocumentationService);
  private queueCleanerStore = inject(QueueCleanerConfigStore);

  // Signals from the store
  readonly queueCleanerConfig = this.queueCleanerStore.config;
  readonly queueCleanerLoading = this.queueCleanerStore.loading;
  readonly queueCleanerSaving = this.queueCleanerStore.saving;
  readonly queueCleanerLoadError = this.queueCleanerStore.loadError;  // Only for "Not connected" state
  readonly queueCleanerSaveError = this.queueCleanerStore.saveError;  // Only for toast notifications

  // Track active accordion tabs
  activeAccordionIndices: number[] = [];

  // Subject for unsubscribing from observables when component is destroyed
  private destroy$ = new Subject<void>();

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return !this.queueCleanerForm.dirty;
  }

  /**
   * Open field-specific documentation in a new tab
   * @param fieldName The form field name (e.g., 'enabled', 'failedImport.maxStrikes')
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('queue-cleaner', fieldName);
  }

  constructor() {
    // Initialize the queue cleaner form with proper disabled states
    this.queueCleanerForm = this.formBuilder.group({
      enabled: [false],
      useAdvancedScheduling: [{ value: false, disabled: true }],
      cronExpression: [{ value: '', disabled: true }, [Validators.required]],
      jobSchedule: this.formBuilder.group({
        every: [{ value: 5, disabled: true }, [Validators.required, Validators.min(1)]],
        type: [{ value: ScheduleUnit.Minutes, disabled: true }],
      }),

      // Failed Import settings - nested group
      failedImport: this.formBuilder.group({
        maxStrikes: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
        ignorePrivate: [{ value: false, disabled: true }],
        deletePrivate: [{ value: false, disabled: true }],
        ignoredPatterns: [{ value: [], disabled: true }],
      }),

      // Stalled settings - nested group
      stalled: this.formBuilder.group({
        maxStrikes: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
        resetStrikesOnProgress: [{ value: false, disabled: true }],
        ignorePrivate: [{ value: false, disabled: true }],
        deletePrivate: [{ value: false, disabled: true }],
        downloadingMetadataMaxStrikes: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
      }),

      // Slow Download settings - nested group
      slow: this.formBuilder.group({
        maxStrikes: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
        resetStrikesOnProgress: [{ value: false, disabled: true }],
        ignorePrivate: [{ value: false, disabled: true }],
        deletePrivate: [{ value: false, disabled: true }],
        minSpeed: [{ value: "", disabled: true }],
        maxTime: [{ value: 0, disabled: true }, [Validators.required, Validators.min(0), Validators.max(1000)]],
        ignoreAboveSize: [{ value: "", disabled: true }],
      }),


    });

    // Create an effect to update the form when the configuration changes
    // Effect to handle configuration changes
    effect(() => {
      const config = this.queueCleanerConfig();
      if (config) {
        // Save original cron expression
        const cronExpression = config.cronExpression;
        
        // Reset form with the config values
        this.queueCleanerForm.patchValue({
          enabled: config.enabled,
          useAdvancedScheduling: config.useAdvancedScheduling || false,
          cronExpression: config.cronExpression,
          jobSchedule: config.jobSchedule || {
            every: 5,
            type: ScheduleUnit.Minutes
          },
          failedImport: config.failedImport,
          stalled: config.stalled,
          slow: config.slow,
        });

        // Then update all other dependent form control states
        this.updateFormControlDisabledStates(config);
        
        // Store original values for dirty checking
        this.storeOriginalValues();

        // Mark form as pristine since we've just loaded the data
        this.queueCleanerForm.markAsPristine();
      }
    });
    
    // Effect to handle load errors - emit to LoadingErrorStateComponent for "Not connected" display
    effect(() => {
      const loadErrorMessage = this.queueCleanerLoadError();
      if (loadErrorMessage) {
        // Load errors should be shown as "Not connected to server" in LoadingErrorStateComponent
        this.error.emit(loadErrorMessage);
      }
    });
    
    // Effect to handle save errors - show as toast notifications for user to fix
    effect(() => {
      const saveErrorMessage = this.queueCleanerSaveError();
      if (saveErrorMessage) {
        // Check if this looks like a validation error from the backend
        // These are typically user-fixable errors that should be shown as toasts
        const isUserFixableError = ErrorHandlerUtil.isUserFixableError(saveErrorMessage);
        
        if (isUserFixableError) {
          // Show validation errors as toast notifications so user can fix them
          this.notificationService.showError(saveErrorMessage);
        } else {
          // For non-user-fixable save errors, also emit to parent
          this.error.emit(saveErrorMessage);
        }
      }
    });
    
    // Set up listeners for form value changes
    this.setupFormValueChangeListeners();
  }

  /**
   * Clean up subscriptions when component is destroyed
   */
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Set up listeners for form control value changes to manage dependent control states
   */
  private setupFormValueChangeListeners(): void {
    // Listen for changes to the 'enabled' control
    const enabledControl = this.queueCleanerForm.get('enabled');
    if (enabledControl) {
      enabledControl.valueChanges.pipe(takeUntil(this.destroy$))
        .subscribe((enabled: boolean) => {
          this.updateMainControlsState(enabled);
        });
    }
      
    // Listen for changes to the 'useAdvancedScheduling' control
    const advancedControl = this.queueCleanerForm.get('useAdvancedScheduling');
    if (advancedControl) {
      advancedControl.valueChanges.pipe(takeUntil(this.destroy$))
        .subscribe((useAdvanced: boolean) => {
          const enabled = this.queueCleanerForm.get('enabled')?.value || false;
          const cronExpressionControl = this.queueCleanerForm.get('cronExpression');
          const jobScheduleGroup = this.queueCleanerForm.get('jobSchedule') as FormGroup;
          const everyControl = jobScheduleGroup?.get('every');
          const typeControl = jobScheduleGroup?.get('type');
          
          // Update scheduling controls based on mode, regardless of enabled state
          if (useAdvanced) {
            if (cronExpressionControl) cronExpressionControl.enable();
            if (everyControl) everyControl.disable();
            if (typeControl) typeControl.disable();
          } else {
            if (cronExpressionControl) cronExpressionControl.disable();
            if (everyControl) everyControl.enable();
            if (typeControl) typeControl.enable();
          }
          
          // Then respect the main enabled state - if disabled, disable all scheduling controls
          if (!enabled) {
            cronExpressionControl?.disable();
            everyControl?.disable();
            typeControl?.disable();
          }
        });
    }

    // Failed import settings
    const failedImportMaxStrikesControl = this.queueCleanerForm.get("failedImport.maxStrikes");
    if (failedImportMaxStrikesControl) {
      failedImportMaxStrikesControl.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe((strikes) => {
        this.updateFailedImportDependentControls(strikes);
      });
    }

    // Stalled settings
    const stalledMaxStrikesControl = this.queueCleanerForm.get("stalled.maxStrikes");
    if (stalledMaxStrikesControl) {
      stalledMaxStrikesControl.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe((strikes) => {
        this.updateStalledDependentControls(strikes);
      });
    }

    // Slow downloads settings
    const slowMaxStrikesControl = this.queueCleanerForm.get("slow.maxStrikes");
    if (slowMaxStrikesControl) {
      slowMaxStrikesControl.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe((strikes) => {
        this.updateSlowDependentControls(strikes);
      });
    }



    // Listen for changes to the schedule type to ensure dropdown isn't empty
    const scheduleTypeControl = this.queueCleanerForm.get('jobSchedule.type');
    if (scheduleTypeControl) {
      scheduleTypeControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => {
          // Ensure the selected value is valid for the new type
          const everyControl = this.queueCleanerForm.get('jobSchedule.every');
          const currentValue = everyControl?.value;
          const scheduleType = this.queueCleanerForm.get('jobSchedule.type')?.value;
          
          const validValues = ScheduleOptions[scheduleType as keyof typeof ScheduleOptions];
          if (validValues && currentValue && !validValues.includes(currentValue)) {
            everyControl?.setValue(validValues[0]);
          }
        });
    }
      
    // Listen to all form changes to check for actual differences from original values
    this.queueCleanerForm.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasActualChanges = this.formValuesChanged();
      });
  }

  /**
   * Store original form values for dirty checking
   */
  private storeOriginalValues(): void {
    // Create a deep copy of the form values to ensure proper comparison
    this.originalFormValues = JSON.parse(JSON.stringify(this.queueCleanerForm.getRawValue()));
    this.hasActualChanges = false;
  }
  
  // Check if the current form values are different from the original values
  private formValuesChanged(): boolean {
    if (!this.originalFormValues) return false;
    
    const currentValues = this.queueCleanerForm.getRawValue();
    return !this.isEqual(currentValues, this.originalFormValues);
  }
  
  // Deep compare two objects for equality
  private isEqual(obj1: any, obj2: any): boolean {
    if (obj1 === obj2) return true;
    
    if (typeof obj1 !== 'object' || obj1 === null ||
        typeof obj2 !== 'object' || obj2 === null) {
      return obj1 === obj2;
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
   * Update form control disabled states based on the configuration
   */
  private updateFormControlDisabledStates(config: QueueCleanerConfig): void {
    // Update main form controls based on the 'enabled' state
    this.updateMainControlsState(config.enabled);

    // Check if failed import strikes are set and update dependent controls
    if (config.failedImport?.maxStrikes !== undefined) {
      this.updateFailedImportDependentControls(config.failedImport.maxStrikes);
    }

    // Check if stalled strikes are set and update dependent controls
    if (config.stalled?.maxStrikes !== undefined) {
      this.updateStalledDependentControls(config.stalled.maxStrikes);
    }

    // Check if slow download strikes are set and update dependent controls
    if (config.slow?.maxStrikes !== undefined) {
      this.updateSlowDependentControls(config.slow.maxStrikes);
    }
  }

  /**
   * Update the state of main controls based on the 'enabled' control value
   */
  private updateMainControlsState(enabled: boolean): void {
    const useAdvancedScheduling = this.queueCleanerForm.get('useAdvancedScheduling')?.value || false;
    const cronExpressionControl = this.queueCleanerForm.get('cronExpression');
    const jobScheduleGroup = this.queueCleanerForm.get('jobSchedule') as FormGroup;
    const everyControl = jobScheduleGroup.get('every');
    const typeControl = jobScheduleGroup.get('type');

    if (enabled) {
      // Enable scheduling controls based on mode
      if (useAdvancedScheduling) {
        cronExpressionControl?.enable();
        everyControl?.disable();
        typeControl?.disable();
      } else {
        cronExpressionControl?.disable();
        everyControl?.enable();
        typeControl?.enable();
      }
      
      // Enable the useAdvancedScheduling control
      const useAdvancedSchedulingControl = this.queueCleanerForm.get('useAdvancedScheduling');
      useAdvancedSchedulingControl?.enable();
      
      // Update individual config sections only if they are enabled
      const failedImportMaxStrikes = this.queueCleanerForm.get("failedImport.maxStrikes")?.value;
      const stalledMaxStrikes = this.queueCleanerForm.get("stalled.maxStrikes")?.value;
      const slowMaxStrikes = this.queueCleanerForm.get("slow.maxStrikes")?.value;
      
      this.updateFailedImportDependentControls(failedImportMaxStrikes);
      this.updateStalledDependentControls(stalledMaxStrikes);
      this.updateSlowDependentControls(slowMaxStrikes);
    } else {
      // Disable all scheduling controls
      cronExpressionControl?.disable();
      everyControl?.disable();
      typeControl?.disable();
      
      // Disable the useAdvancedScheduling control
      const useAdvancedSchedulingControl = this.queueCleanerForm.get('useAdvancedScheduling');
      useAdvancedSchedulingControl?.disable();
      
      // Save current active accordion state before clearing it
      // This will be empty when we collapse all accordions
      this.activeAccordionIndices = [];
    }
  }

  /**
   * Update the state of Failed Import dependent controls based on the 'maxStrikes' value
   */
  private updateFailedImportDependentControls(strikes: number): void {
    const enable = strikes >= 3;
    const options = { onlySelf: true };

    if (enable) {
      this.queueCleanerForm.get("failedImport")?.get("ignorePrivate")?.enable(options);
      this.queueCleanerForm.get("failedImport")?.get("deletePrivate")?.enable(options);
      this.queueCleanerForm.get("failedImport")?.get("ignoredPatterns")?.enable(options);
    } else {
      this.queueCleanerForm.get("failedImport")?.get("ignorePrivate")?.disable(options);
      this.queueCleanerForm.get("failedImport")?.get("deletePrivate")?.disable(options);
      this.queueCleanerForm.get("failedImport")?.get("ignoredPatterns")?.disable(options);
    }
  }

  /**
   * Update the state of Stalled dependent controls based on the 'maxStrikes' value
   */
  private updateStalledDependentControls(strikes: number): void {
    const enable = strikes >= 3;
    const options = { onlySelf: true };

    if (enable) {
      this.queueCleanerForm.get("stalled")?.get("resetStrikesOnProgress")?.enable(options);
      this.queueCleanerForm.get("stalled")?.get("ignorePrivate")?.enable(options);
      this.queueCleanerForm.get("stalled")?.get("deletePrivate")?.enable(options);
    } else {
      this.queueCleanerForm.get("stalled")?.get("resetStrikesOnProgress")?.disable(options);
      this.queueCleanerForm.get("stalled")?.get("ignorePrivate")?.disable(options);
      this.queueCleanerForm.get("stalled")?.get("deletePrivate")?.disable(options);
    }
  }

  /**
   * Update the state of Slow Download dependent controls based on the 'maxStrikes' value
   */
  private updateSlowDependentControls(strikes: number): void {
    const enable = strikes >= 3;
    const options = { onlySelf: true };

    if (enable) {
      this.queueCleanerForm.get("slow")?.get("resetStrikesOnProgress")?.enable(options);
      this.queueCleanerForm.get("slow")?.get("ignorePrivate")?.enable(options);
      this.queueCleanerForm.get("slow")?.get("deletePrivate")?.enable(options);
      this.queueCleanerForm.get("slow")?.get("minSpeed")?.enable(options);
      this.queueCleanerForm.get("slow")?.get("maxTime")?.enable(options);
      this.queueCleanerForm.get("slow")?.get("ignoreAboveSize")?.enable(options);
    } else {
      this.queueCleanerForm.get("slow")?.get("resetStrikesOnProgress")?.disable(options);
      this.queueCleanerForm.get("slow")?.get("ignorePrivate")?.disable(options);
      this.queueCleanerForm.get("slow")?.get("deletePrivate")?.disable(options);
      this.queueCleanerForm.get("slow")?.get("minSpeed")?.disable(options);
      this.queueCleanerForm.get("slow")?.get("maxTime")?.disable(options);
      this.queueCleanerForm.get("slow")?.get("ignoreAboveSize")?.disable(options);
    }
  }



  /**
   * Save the queue cleaner configuration
   */
  saveQueueCleanerConfig(): void {
    // Mark all form controls as touched to trigger validation messages
    this.markFormGroupTouched(this.queueCleanerForm);
    
    if (this.queueCleanerForm.valid) {
      // Make a copy of the form values
      const formValue = this.queueCleanerForm.getRawValue();
      
      // Determine the correct cron expression to use
      const cronExpression: string = formValue.useAdvancedScheduling ? 
        formValue.cronExpression : 
        // If in basic mode, generate cron expression from the schedule
        this.queueCleanerStore.generateCronExpression(formValue.jobSchedule);
      
      // Create the config object to be saved
      const queueCleanerConfig: QueueCleanerConfig = {
        enabled: formValue.enabled,
        useAdvancedScheduling: formValue.useAdvancedScheduling,
        cronExpression: cronExpression,
        jobSchedule: formValue.jobSchedule,
        failedImport: {
          maxStrikes: formValue.failedImport?.maxStrikes || 0,
          ignorePrivate: formValue.failedImport?.ignorePrivate || false,
          deletePrivate: formValue.failedImport?.deletePrivate || false,
          ignoredPatterns: formValue.failedImport?.ignoredPatterns || [],
        },
        stalled: {
          maxStrikes: formValue.stalled?.maxStrikes || 0,
          resetStrikesOnProgress: formValue.stalled?.resetStrikesOnProgress || false,
          ignorePrivate: formValue.stalled?.ignorePrivate || false,
          deletePrivate: formValue.stalled?.deletePrivate || false,
          downloadingMetadataMaxStrikes: formValue.stalled?.downloadingMetadataMaxStrikes || 0,
        },
        slow: {
          maxStrikes: formValue.slow?.maxStrikes || 0,
          resetStrikesOnProgress: formValue.slow?.resetStrikesOnProgress || false,
          ignorePrivate: formValue.slow?.ignorePrivate || false,
          deletePrivate: formValue.slow?.deletePrivate || false,
          minSpeed: formValue.slow?.minSpeed || "",
          maxTime: formValue.slow?.maxTime || 0,
          ignoreAboveSize: formValue.slow?.ignoreAboveSize || "",
        },
      };
      
      // Save the configuration
      this.queueCleanerStore.saveConfig(queueCleanerConfig);
      
      // Setup a one-time check to mark form as pristine after successful save
      // This pattern works with signals since we're not trying to pipe the signal itself
      const checkSaveCompletion = () => {
        const saving = this.queueCleanerSaving();
        const saveError = this.queueCleanerSaveError();
        
        if (!saving && !saveError) {
          // Mark form as pristine after successful save
          this.queueCleanerForm.markAsPristine();
          // Update original values reference
          this.storeOriginalValues();
          // Emit saved event 
          this.saved.emit();
          // Display success message
          this.notificationService.showSuccess('Queue cleaner configuration saved successfully.');
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
    } else {
      // Form is invalid, show error message
      this.notificationService.showValidationError();
      
      // Emit error for parent components
      this.error.emit("Please fix validation errors before saving.");
    }
  }


  
  /**
   * Reset the queue cleaner configuration form to default values
   */
  resetQueueCleanerConfig(): void {  
    this.queueCleanerForm.reset({
      enabled: false,
      useAdvancedScheduling: false,
      cronExpression: "0 0/5 * * * ?",
      jobSchedule: {
        every: 5,
        type: ScheduleUnit.Minutes,
      },

      // Failed Import settings (nested)
      failedImport: {
        maxStrikes: 0,
        ignorePrivate: false,
        deletePrivate: false,
        ignoredPatterns: [],
      },

      // Stalled settings (nested)
      stalled: {
        maxStrikes: 0,
        resetStrikesOnProgress: false,
        ignorePrivate: false,
        deletePrivate: false,
        downloadingMetadataMaxStrikes: 0,
      },

      // Slow Download settings (nested)
      slow: {
        maxStrikes: 0,
        resetStrikesOnProgress: false,
        ignorePrivate: false,
        deletePrivate: false,
        minSpeed: "",
        maxTime: 0,
        ignoreAboveSize: "",
      },


    });

    // Manually update control states after reset
    this.updateMainControlsState(false);
    this.updateFailedImportDependentControls(0);
    this.updateStalledDependentControls(0);
    this.updateSlowDependentControls(0);
    
    // Mark form as dirty so the save button is enabled after reset
    this.queueCleanerForm.markAsDirty();
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
    const control = this.queueCleanerForm.get(controlName);
    return control ? control.dirty && control.hasError(errorName) : false;
  }
  
  /**
   * Get schedule value options based on the current schedule unit type
   */
  getScheduleValueOptions(): {label: string, value: number}[] {
    const scheduleType = this.queueCleanerForm.get('jobSchedule.type')?.value as ScheduleUnit;
    if (scheduleType === ScheduleUnit.Seconds) {
      return this.scheduleValueOptions[ScheduleUnit.Seconds];
    } else if (scheduleType === ScheduleUnit.Minutes) {
      return this.scheduleValueOptions[ScheduleUnit.Minutes];
    } else if (scheduleType === ScheduleUnit.Hours) {
      return this.scheduleValueOptions[ScheduleUnit.Hours];
    }
    return this.scheduleValueOptions[ScheduleUnit.Minutes]; // Default to minutes
  }

  /**
   * Get nested form control errors
   */
  hasNestedError(parentName: string, controlName: string, errorName: string): boolean {
    const parentControl = this.queueCleanerForm.get(parentName);
    if (!parentControl || !(parentControl instanceof FormGroup)) {
      return false;
    }

    const control = parentControl.get(controlName);
    return control ? control.dirty && control.hasError(errorName) : false;
  }
  

}
