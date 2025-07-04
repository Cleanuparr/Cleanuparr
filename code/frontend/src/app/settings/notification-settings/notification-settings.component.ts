import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { NotificationConfigStore } from "./notification-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import { NotificationsConfig } from "../../shared/models/notifications-config.model";

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
  selector: "app-notification-settings",
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
  providers: [NotificationConfigStore],
  templateUrl: "./notification-settings.component.html",
  styleUrls: ["./notification-settings.component.scss"],
})
export class NotificationSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Notification Configuration Form
  notificationForm: FormGroup;
  
  // Original form values for tracking changes
  private originalFormValues: any;
  
  // Track whether the form has actual changes compared to original values
  hasActualChanges = false;

  // Inject the necessary services
  private formBuilder = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private documentationService = inject(DocumentationService);
  private notificationConfigStore = inject(NotificationConfigStore);

  // Signals from the store
  readonly notificationConfig = this.notificationConfigStore.config;
  readonly notificationLoading = this.notificationConfigStore.loading;
  readonly notificationSaving = this.notificationConfigStore.saving;
  readonly notificationError = this.notificationConfigStore.error;

  // Subject for unsubscribing from observables when component is destroyed
  private destroy$ = new Subject<void>();

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return !this.notificationForm.dirty;
  }

  constructor() {
    // Initialize the notification settings form
    this.notificationForm = this.formBuilder.group({
      // Notifiarr configuration
      notifiarr: this.formBuilder.group({
        apiKey: [''],
        channelId: [''],
        onFailedImportStrike: [false],
        onStalledStrike: [false],
        onSlowStrike: [false],
        onQueueItemDeleted: [false],
        onDownloadCleaned: [false],
        onCategoryChanged: [false],
      }),
      // Apprise configuration
      apprise: this.formBuilder.group({
        fullUrl: [''],
        key: [''],
        tags: [''],
        onFailedImportStrike: [false],
        onStalledStrike: [false],
        onSlowStrike: [false],
        onQueueItemDeleted: [false],
        onDownloadCleaned: [false],
        onCategoryChanged: [false],
      }),
    });

    // Setup effect to react to config changes
    effect(() => {
      const config = this.notificationConfig();
      if (config) {
        // Map the server response to form values
        const formValue = {
          notifiarr: config.notifiarr || {
            apiKey: '',
            channelId: '',
            onFailedImportStrike: false,
            onStalledStrike: false,
            onSlowStrike: false,
            onQueueItemDeleted: false,
            onDownloadCleaned: false,
            onCategoryChanged: false,
          },
          apprise: config.apprise || {
            fullUrl: '',
            key: '',
            tags: '',
            onFailedImportStrike: false,
            onStalledStrike: false,
            onSlowStrike: false,
            onQueueItemDeleted: false,
            onDownloadCleaned: false,
            onCategoryChanged: false,
          },
        };

        this.notificationForm.patchValue(formValue);
        this.storeOriginalValues();
        this.notificationForm.markAsPristine();
        this.hasActualChanges = false;
      }
    });

    // Track form changes for dirty state
    this.notificationForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasActualChanges = this.formValuesChanged();
      });

    // Setup effect to react to error changes
    effect(() => {
      const errorMessage = this.notificationError();
      if (errorMessage) {
        // Only emit the error for parent components
        this.error.emit(errorMessage);
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
   * Check if the current form values are different from the original values
   */
  private formValuesChanged(): boolean {
    return !this.isEqual(this.notificationForm.value, this.originalFormValues);
  }

  /**
   * Deep compare two objects for equality
   */
  private isEqual(obj1: any, obj2: any): boolean {
    if (obj1 === obj2) return true;
    if (obj1 === null || obj2 === null) return false;
    if (obj1 === undefined || obj2 === undefined) return false;
    
    if (typeof obj1 !== 'object' && typeof obj2 !== 'object') {
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
      if (!this.isEqual(obj1[key], obj2[key])) return false;
    }
    
    return true;
  }

  /**
   * Store original form values for dirty checking
   */
  private storeOriginalValues(): void {
    this.originalFormValues = JSON.parse(JSON.stringify(this.notificationForm.value));
  }

  /**
   * Save the notification configuration
   */
  saveNotificationConfig(): void {
    if (this.notificationForm.invalid) {
      this.markFormGroupTouched(this.notificationForm);
      this.notificationService.showValidationError();
      return;
    }

    if (!this.hasActualChanges) {
      this.notificationService.showSuccess('No changes detected');
      return;
    }

    const formValues = this.notificationForm.value;

    const config: NotificationsConfig = {
      notifiarr: formValues.notifiarr,
      apprise: formValues.apprise,
    };

    // Save the configuration
    this.notificationConfigStore.saveConfig(config);
    
    // Setup a one-time check to mark form as pristine after successful save
    const checkSaveCompletion = () => {
      const loading = this.notificationSaving();
      const error = this.notificationError();
      
      if (!loading && !error) {
        // Mark form as pristine after successful save
        this.notificationForm.markAsPristine();
        this.hasActualChanges = false;
        
        // Emit saved event
        this.saved.emit();
        // Show success message
        this.notificationService.showSuccess('Notification configuration saved successfully!');
      } else if (!loading && error) {
        // If there's an error, we can stop checking
      } else {
        // If still loading, check again in a moment
        setTimeout(checkSaveCompletion, 100);
      }
    };
    
    // Start checking for save completion
    checkSaveCompletion();
  }

  /**
   * Reset the notification configuration form to default values
   */
  resetNotificationConfig(): void {
    this.notificationForm.reset({
      notifiarr: {
        apiKey: '',
        channelId: '',
        onFailedImportStrike: false,
        onStalledStrike: false,
        onSlowStrike: false,
        onQueueItemDeleted: false,
        onDownloadCleaned: false,
        onCategoryChanged: false,
      },
      apprise: {
        fullUrl: '',
        key: '',
        tags: '',
        onFailedImportStrike: false,
        onStalledStrike: false,
        onSlowStrike: false,
        onQueueItemDeleted: false,
        onDownloadCleaned: false,
        onCategoryChanged: false,
      },
    });
    
    // Check if this reset actually changes anything compared to the original state
    const hasChangesAfterReset = this.formValuesChanged();
    
    if (hasChangesAfterReset) {
      // Only mark as dirty if the reset actually changes something
      this.notificationForm.markAsDirty();
      this.hasActualChanges = true;
    } else {
      // If reset brings us back to original state, mark as pristine
      this.notificationForm.markAsPristine();
      this.hasActualChanges = false;
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
   * Check if a form control has an error after it's been touched
   */
  hasError(controlName: string, errorName: string): boolean {
    const control = this.notificationForm.get(controlName);
    return control ? control.dirty && control.hasError(errorName) : false;
  }

  /**
   * Check if a nested form control has an error after it's been touched
   */
  hasNestedError(groupName: string, controlName: string, errorName: string): boolean {
    const control = this.notificationForm.get(`${groupName}.${controlName}`);
    return control ? control.dirty && control.hasError(errorName) : false;
  }

  /**
   * Opens documentation for a specific field
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications', fieldName);
  }
} 
