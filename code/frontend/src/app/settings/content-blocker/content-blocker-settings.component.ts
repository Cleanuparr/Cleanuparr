import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { ContentBlockerConfigStore } from "./content-blocker-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import {
  ContentBlockerConfig,
  ScheduleUnit,
  BlocklistType,
  ScheduleOptions
} from "../../shared/models/content-blocker-config.model";
import { FluidModule } from 'primeng/fluid';


// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { AccordionModule } from "primeng/accordion";
import { SelectButtonModule } from "primeng/selectbutton";
import { ToastModule } from "primeng/toast";
// Using centralized NotificationService instead of MessageService
import { NotificationService } from "../../core/services/notification.service";
import { SelectModule } from "primeng/select";
import { DropdownModule } from "primeng/dropdown";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";
import { ErrorHandlerUtil } from "../../core/utils/error-handler.util";
import { DocumentationService } from "../../core/services/documentation.service";

@Component({
  selector: "app-content-blocker-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    AccordionModule,
    SelectButtonModule,
    ToastModule,
    SelectModule,
    DropdownModule,
    LoadingErrorStateComponent,
    FluidModule,
  ],
  providers: [ContentBlockerConfigStore],
  templateUrl: "./content-blocker-settings.component.html",
  styleUrls: ["./content-blocker-settings.component.scss"],
})
export class ContentBlockerSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Content Blocker Configuration Form
  contentBlockerForm: FormGroup;
  
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
  private contentBlockerStore = inject(ContentBlockerConfigStore);
  private documentationService = inject(DocumentationService);

  // Signals from the store
  readonly contentBlockerConfig = this.contentBlockerStore.config;
  readonly contentBlockerLoading = this.contentBlockerStore.loading;
  readonly contentBlockerSaving = this.contentBlockerStore.saving;
  readonly contentBlockerLoadError = this.contentBlockerStore.loadError;  // Only for "Not connected" state
  readonly contentBlockerSaveError = this.contentBlockerStore.saveError;  // Only for toast notifications

  // Track active accordion tabs
  activeAccordionIndices: number[] = [];

  // Subject for unsubscribing from observables when component is destroyed
  private destroy$ = new Subject<void>();

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return !this.contentBlockerForm.dirty;
  }

  /**
   * Opens field-specific documentation
   * @param fieldName Field name to open documentation for
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('content-blocker', fieldName);
  }

  constructor() {
    // Initialize the content blocker form with proper disabled states
    this.contentBlockerForm = this.formBuilder.group({
      enabled: [false],
      useAdvancedScheduling: [{ value: false, disabled: true }],
      cronExpression: [{ value: '', disabled: true }, [Validators.required]],
      jobSchedule: this.formBuilder.group({
        every: [{ value: 5, disabled: true }, [Validators.required, Validators.min(1)]],
        type: [{ value: ScheduleUnit.Seconds, disabled: true }],
      }),

      ignorePrivate: [{ value: false, disabled: true }],
      deletePrivate: [{ value: false, disabled: true }],

      // Blocklist settings for each Arr
      sonarr: this.formBuilder.group({
        enabled: [{ value: false, disabled: true }],
        blocklistPath: [{ value: "", disabled: true }],
        blocklistType: [{ value: BlocklistType.Blacklist, disabled: true }],
      }),
      radarr: this.formBuilder.group({
        enabled: [{ value: false, disabled: true }],
        blocklistPath: [{ value: "", disabled: true }],
        blocklistType: [{ value: BlocklistType.Blacklist, disabled: true }],
      }),
      lidarr: this.formBuilder.group({
        enabled: [{ value: false, disabled: true }],
        blocklistPath: [{ value: "", disabled: true }],
        blocklistType: [{ value: BlocklistType.Blacklist, disabled: true }],
      }),
      readarr: this.formBuilder.group({
        enabled: [{ value: false, disabled: true }],
        blocklistPath: [{ value: "", disabled: true }],
        blocklistType: [{ value: BlocklistType.Blacklist, disabled: true }],
      }),
      whisparr: this.formBuilder.group({
        enabled: [{ value: false, disabled: true }],
        blocklistPath: [{ value: "", disabled: true }],
        blocklistType: [{ value: BlocklistType.Blacklist, disabled: true }],
      }),
    });

    // Create an effect to update the form when the configuration changes
    effect(() => {
      const config = this.contentBlockerConfig();
      if (config) {
        // Reset form with the config values
        this.contentBlockerForm.patchValue({
          enabled: config.enabled,
          useAdvancedScheduling: config.useAdvancedScheduling || false,
          cronExpression: config.cronExpression,
          jobSchedule: config.jobSchedule || {
            every: 5,
            type: ScheduleUnit.Seconds
          },
          ignorePrivate: config.ignorePrivate,
          deletePrivate: config.deletePrivate,
          sonarr: config.sonarr,
          radarr: config.radarr,
          lidarr: config.lidarr,
          readarr: config.readarr,
          whisparr: config.whisparr,
        });

        // Update all form control states
        this.updateFormControlDisabledStates(config);
        
        // Store original values for dirty checking
        this.storeOriginalValues();

        // Mark form as pristine since we've just loaded the data
        this.contentBlockerForm.markAsPristine();
      }
    });
    
    // Effect to handle load errors - emit to LoadingErrorStateComponent for "Not connected" display
    effect(() => {
      const loadErrorMessage = this.contentBlockerLoadError();
      if (loadErrorMessage) {
        // Load errors should be shown as "Not connected to server" in LoadingErrorStateComponent
        this.error.emit(loadErrorMessage);
      }
    });
    
    // Effect to handle save errors - show as toast notifications for user to fix
    effect(() => {
      const saveErrorMessage = this.contentBlockerSaveError();
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
    const enabledControl = this.contentBlockerForm.get('enabled');
    if (enabledControl) {
      enabledControl.valueChanges.pipe(takeUntil(this.destroy$))
        .subscribe((enabled: boolean) => {
          this.updateMainControlsState(enabled);
        });
    }
      
    // Listen for changes to the 'useAdvancedScheduling' control
    const advancedControl = this.contentBlockerForm.get('useAdvancedScheduling');
    if (advancedControl) {
      advancedControl.valueChanges.pipe(takeUntil(this.destroy$))
        .subscribe((useAdvanced: boolean) => {
          const enabled = this.contentBlockerForm.get('enabled')?.value || false;
          if (enabled) {
            const cronExpressionControl = this.contentBlockerForm.get('cronExpression');
            const jobScheduleGroup = this.contentBlockerForm.get('jobSchedule') as FormGroup;
            const everyControl = jobScheduleGroup?.get('every');
            const typeControl = jobScheduleGroup?.get('type');
            
            if (useAdvanced) {
              if (cronExpressionControl) cronExpressionControl.enable();
              if (everyControl) everyControl.disable();
              if (typeControl) typeControl.disable();
            } else {
              if (cronExpressionControl) cronExpressionControl.disable();
              if (everyControl) everyControl.enable();
              if (typeControl) typeControl.enable();
            }
          }
        });
    }

    // Listen for changes to the schedule type to ensure dropdown isn't empty
    const scheduleTypeControl = this.contentBlockerForm.get('jobSchedule.type');
    if (scheduleTypeControl) {
      scheduleTypeControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => {
          // Ensure the selected value is valid for the new type
          const everyControl = this.contentBlockerForm.get('jobSchedule.every');
          const currentValue = everyControl?.value;
          const scheduleType = this.contentBlockerForm.get('jobSchedule.type')?.value;
          
          const validValues = ScheduleOptions[scheduleType as keyof typeof ScheduleOptions];
          if (currentValue && !validValues.includes(currentValue)) {
            everyControl?.setValue(validValues[0]);
          }
        });
    }
      
    // Listen for changes to blocklist enabled states
    ['sonarr', 'radarr', 'lidarr', 'readarr', 'whisparr'].forEach(arrType => {
      const enabledControl = this.contentBlockerForm.get(`${arrType}.enabled`);
      
      if (enabledControl) {
        enabledControl.valueChanges.pipe(takeUntil(this.destroy$))
          .subscribe((enabled: boolean) => {
            this.updateBlocklistDependentControls(arrType, enabled);
          });
      }
    });
    
    // Listen to all form changes to check for actual differences from original values
    this.contentBlockerForm.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasActualChanges = this.formValuesChanged();
      });
  }

  /**
   * Store original form values for dirty checking
   */
  private storeOriginalValues(): void {
    // Create a deep copy of the form values to ensure proper comparison
    this.originalFormValues = JSON.parse(JSON.stringify(this.contentBlockerForm.getRawValue()));
    this.hasActualChanges = false;
  }
  
  // Check if the current form values are different from the original values
  private formValuesChanged(): boolean {
    if (!this.originalFormValues) return false;
    
    const currentValues = this.contentBlockerForm.getRawValue();
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
  private updateFormControlDisabledStates(config: ContentBlockerConfig): void {
    // Update main form controls based on the 'enabled' state
    this.updateMainControlsState(config.enabled);

    // Update blocklist dependent controls if the main feature is enabled
    if (config.enabled) {
      this.updateBlocklistDependentControls('sonarr', config.sonarr?.enabled || false);
      this.updateBlocklistDependentControls('radarr', config.radarr?.enabled || false);
      this.updateBlocklistDependentControls('lidarr', config.lidarr?.enabled || false);
      this.updateBlocklistDependentControls('readarr', config.readarr?.enabled || false);
      this.updateBlocklistDependentControls('whisparr', config.whisparr?.enabled || false);
    }
  }

  /**
   * Update the state of blocklist dependent controls based on the 'enabled' control value
   */
  private updateBlocklistDependentControls(arrType: string, enabled: boolean): void {
    const pathControl = this.contentBlockerForm.get(`${arrType}.blocklistPath`);
    const typeControl = this.contentBlockerForm.get(`${arrType}.blocklistType`);
    const options = { onlySelf: true };

    if (enabled) {
      // Enable dependent controls and set validation
      pathControl?.enable(options);
      typeControl?.enable(options);
      pathControl?.setValidators([Validators.required]);
    } else {
      // Disable dependent controls and clear validation
      pathControl?.disable(options);
      typeControl?.disable(options);
      pathControl?.clearValidators();
    }
    pathControl?.updateValueAndValidity();
  }

  /**
   * Update the state of main controls based on the 'enabled' control value
   */
  private updateMainControlsState(enabled: boolean): void {
    const useAdvancedScheduling = this.contentBlockerForm.get('useAdvancedScheduling')?.value || false;
    const cronExpressionControl = this.contentBlockerForm.get('cronExpression');
    const jobScheduleGroup = this.contentBlockerForm.get('jobSchedule') as FormGroup;
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
      const useAdvancedSchedulingControl = this.contentBlockerForm.get('useAdvancedScheduling');
      useAdvancedSchedulingControl?.enable();
      
      // Enable content blocker specific controls
      this.contentBlockerForm.get("ignorePrivate")?.enable({ onlySelf: true });
      this.contentBlockerForm.get("deletePrivate")?.enable({ onlySelf: true });

      // Enable blocklist settings for each Arr
      this.contentBlockerForm.get("sonarr.enabled")?.enable({ onlySelf: true });
      this.contentBlockerForm.get("radarr.enabled")?.enable({ onlySelf: true });
      this.contentBlockerForm.get("lidarr.enabled")?.enable({ onlySelf: true });
      this.contentBlockerForm.get("readarr.enabled")?.enable({ onlySelf: true });
      this.contentBlockerForm.get("whisparr.enabled")?.enable({ onlySelf: true });
      
      // Update dependent controls based on current enabled states
      const sonarrEnabled = this.contentBlockerForm.get("sonarr.enabled")?.value || false;
      const radarrEnabled = this.contentBlockerForm.get("radarr.enabled")?.value || false;
      const lidarrEnabled = this.contentBlockerForm.get("lidarr.enabled")?.value || false;
      const readarrEnabled = this.contentBlockerForm.get("readarr.enabled")?.value || false;
      const whisparrEnabled = this.contentBlockerForm.get("whisparr.enabled")?.value || false;
      
      this.updateBlocklistDependentControls('sonarr', sonarrEnabled);
      this.updateBlocklistDependentControls('radarr', radarrEnabled);
      this.updateBlocklistDependentControls('lidarr', lidarrEnabled);
      this.updateBlocklistDependentControls('readarr', readarrEnabled);
      this.updateBlocklistDependentControls('whisparr', whisparrEnabled);
    } else {
      // Disable all scheduling controls
      cronExpressionControl?.disable();
      everyControl?.disable();
      typeControl?.disable();
      
      // Disable the useAdvancedScheduling control
      const useAdvancedSchedulingControl = this.contentBlockerForm.get('useAdvancedScheduling');
      useAdvancedSchedulingControl?.disable();
      
      // Disable content blocker specific controls
      this.contentBlockerForm.get("ignorePrivate")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("deletePrivate")?.disable({ onlySelf: true });

      // Disable all blocklist settings for each Arr
      this.contentBlockerForm.get("sonarr.enabled")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("sonarr.blocklistPath")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("sonarr.blocklistType")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("radarr.enabled")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("radarr.blocklistPath")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("radarr.blocklistType")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("lidarr.enabled")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("lidarr.blocklistPath")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("lidarr.blocklistType")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("readarr.enabled")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("readarr.blocklistPath")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("readarr.blocklistType")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("whisparr.enabled")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("whisparr.blocklistPath")?.disable({ onlySelf: true });
      this.contentBlockerForm.get("whisparr.blocklistType")?.disable({ onlySelf: true });

      // Save current active accordion state before clearing it
      this.activeAccordionIndices = [];
    }
  }

  /**
   * Save the content blocker configuration
   */
  saveContentBlockerConfig(): void {
    // Mark all form controls as touched to trigger validation messages
    this.markFormGroupTouched(this.contentBlockerForm);
    
    if (this.contentBlockerForm.valid) {
      // Make a copy of the form values
      const formValue = this.contentBlockerForm.getRawValue();
      
      // Create the config object to be saved
      const contentBlockerConfig: ContentBlockerConfig = {
        enabled: formValue.enabled,
        useAdvancedScheduling: formValue.useAdvancedScheduling,
        cronExpression: formValue.useAdvancedScheduling ? 
          formValue.cronExpression : 
          // If in basic mode, generate cron expression from the schedule
          this.contentBlockerStore.generateCronExpression(formValue.jobSchedule),
        jobSchedule: formValue.jobSchedule,
        ignorePrivate: formValue.ignorePrivate || false,
        deletePrivate: formValue.deletePrivate || false,
        sonarr: formValue.sonarr || {
          enabled: false,
          blocklistPath: "",
          blocklistType: BlocklistType.Blacklist,
        },
        radarr: formValue.radarr || {
          enabled: false,
          blocklistPath: "",
          blocklistType: BlocklistType.Blacklist,
        },
        lidarr: formValue.lidarr || {
          enabled: false,
          blocklistPath: "",
          blocklistType: BlocklistType.Blacklist,
        },
        readarr: formValue.readarr || {
          enabled: false,
          blocklistPath: "",
          blocklistType: BlocklistType.Blacklist,
        },
        whisparr: formValue.whisparr || {
          enabled: false,
          blocklistPath: "",
          blocklistType: BlocklistType.Blacklist,
        },
      };
      
      // Save the configuration
      this.contentBlockerStore.saveConfig(contentBlockerConfig);
      
      // Setup a one-time check to mark form as pristine after successful save
      const checkSaveCompletion = () => {
        const saving = this.contentBlockerSaving();
        const saveError = this.contentBlockerSaveError();
        
        if (!saving && !saveError) {
          // Mark form as pristine after successful save
          this.contentBlockerForm.markAsPristine();
          // Update original values reference
          this.storeOriginalValues();
          // Emit saved event 
          this.saved.emit();
          // Display success message
          this.notificationService.showSuccess('Content blocker configuration saved successfully.');
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
   * Reset the content blocker configuration form to default values
   */
  resetContentBlockerConfig(): void {  
    this.contentBlockerForm.reset({
      enabled: false,
      useAdvancedScheduling: false,
      cronExpression: "0/5 * * * * ?",
      jobSchedule: {
        every: 5,
        type: ScheduleUnit.Seconds,
      },
      ignorePrivate: false,
      deletePrivate: false,
      sonarr: {
        enabled: false,
        blocklistPath: "",
        blocklistType: BlocklistType.Blacklist,
      },
      radarr: {
        enabled: false,
        blocklistPath: "",
        blocklistType: BlocklistType.Blacklist,
      },
      lidarr: {
        enabled: false,
        blocklistPath: "",
        blocklistType: BlocklistType.Blacklist,
      },
      readarr: {
        enabled: false,
        blocklistPath: "",
        blocklistType: BlocklistType.Blacklist,
      },
      whisparr: {
        enabled: false,
        blocklistPath: "",
        blocklistType: BlocklistType.Blacklist,
      },
    });

    // Manually update control states after reset
    this.updateMainControlsState(false);
    this.updateBlocklistDependentControls('sonarr', false);
    this.updateBlocklistDependentControls('radarr', false);
    this.updateBlocklistDependentControls('lidarr', false);
    this.updateBlocklistDependentControls('readarr', false);
    this.updateBlocklistDependentControls('whisparr', false);
    
    // Mark form as dirty so the save button is enabled after reset
    this.contentBlockerForm.markAsDirty();
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
    const control = this.contentBlockerForm.get(controlName);
    return control ? control.dirty && control.hasError(errorName) : false;
  }
  
  /**
   * Get schedule value options based on the current schedule unit type
   */
  getScheduleValueOptions(): {label: string, value: number}[] {
    const scheduleType = this.contentBlockerForm.get('jobSchedule.type')?.value as ScheduleUnit;
    if (scheduleType === ScheduleUnit.Seconds) {
      return this.scheduleValueOptions[ScheduleUnit.Seconds];
    } else if (scheduleType === ScheduleUnit.Minutes) {
      return this.scheduleValueOptions[ScheduleUnit.Minutes];
    } else if (scheduleType === ScheduleUnit.Hours) {
      return this.scheduleValueOptions[ScheduleUnit.Hours];
    }
    return this.scheduleValueOptions[ScheduleUnit.Seconds]; // Default to seconds
  }

  /**
   * Get nested form control errors
   */
  hasNestedError(parentName: string, controlName: string, errorName: string): boolean {
    const parentControl = this.contentBlockerForm.get(parentName);
    if (!parentControl || !(parentControl instanceof FormGroup)) {
      return false;
    }

    const control = parentControl.get(controlName);
    return control ? control.dirty && control.hasError(errorName) : false;
  }
  

} 
