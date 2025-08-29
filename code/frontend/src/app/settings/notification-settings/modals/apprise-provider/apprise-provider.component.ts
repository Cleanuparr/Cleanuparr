import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { AppriseFormData } from '../../models/provider-modal.model';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';

@Component({
  selector: 'app-apprise-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    CheckboxModule,
    TooltipModule
  ],
  template: `
    <p-dialog 
      [(visible)]="visible" 
      [modal]="true" 
      [closable]="true"
      [draggable]="false"
      [resizable]="false"
      styleClass="instance-modal"
      header="Configure Apprise Provider"
      (onHide)="onCancel()">
      
      <form [formGroup]="providerForm" class="p-fluid instance-form">
        <!-- Enabled Toggle -->
        <div class="field flex flex-row">
          <label class="field-label">
            <i class="pi pi-question-circle field-info-icon" 
               pTooltip="Enable or disable this notification provider"></i>
            Enabled
          </label>
          <div class="field-input">
            <p-checkbox formControlName="enabled" [binary]="true"></p-checkbox>
            <small class="form-helper-text">Enable this notification provider</small>
          </div>
        </div>

        <!-- Provider Name -->
        <div class="field">
          <label for="provider-name">
            <i class="pi pi-question-circle field-info-icon" 
               pTooltip="A unique name to identify this provider"></i>
            Provider Name *
          </label>
          <input 
            id="provider-name"
            type="text" 
            pInputText 
            formControlName="name" 
            placeholder="My Apprise Provider"
            class="w-full" />
          <small *ngIf="hasError('name', 'required')" class="p-error">
            Provider name is required
          </small>
        </div>

        <!-- Apprise-specific Configuration -->
        <div class="apprise-fields">
          <!-- Apprise URL Field -->
          <div class="field">
            <label for="fullUrl" class="field-label">
              <i class="pi pi-globe"></i>
              Apprise Server URL *
            </label>
            <input
              id="fullUrl"
              type="url"
              pInputText
              formControlName="fullUrl"
              placeholder="http://localhost:8000"
              [class.ng-invalid]="hasError('fullUrl', 'required')"
              class="w-full" />
            <small class="field-help">
              The URL of your Apprise server where notifications will be sent.
            </small>
            <small *ngIf="hasError('fullUrl', 'required')" class="p-error">
              Apprise server URL is required
            </small>
            <small *ngIf="hasError('fullUrl', 'pattern')" class="p-error">
              Please enter a valid URL (e.g., http://localhost:8000)
            </small>
          </div>

          <!-- Key Field -->
          <div class="field">
            <label for="key" class="field-label">
              <i class="pi pi-key"></i>
              Configuration Key *
            </label>
            <input
              id="key"
              type="text"
              pInputText
              formControlName="key"
              placeholder="my-config-key"
              [class.ng-invalid]="hasError('key', 'required')"
              class="w-full" />
            <small class="field-help">
              The key that identifies your Apprise configuration on the server.
            </small>
            <small *ngIf="hasError('key', 'required')" class="p-error">
              Configuration key is required
            </small>
            <small *ngIf="hasError('key', 'minlength')" class="p-error">
              Key must be at least 2 characters
            </small>
          </div>

          <!-- Tags Field -->
          <div class="field">
            <label for="tags" class="field-label">
              <i class="pi pi-tags"></i>
              Tags (Optional)
            </label>
            <input
              id="tags"
              type="text"
              pInputText
              formControlName="tags"
              placeholder="tag1,tag2 or tag3 tag4"
              class="w-full" />
            <small class="field-help">
              Optional tags to filter notifications. Use comma (,) to OR tags and space ( ) to AND them.
            </small>
          </div>
        </div>

        <!-- Event Triggers Section -->
        <div class="field">
          <label>
            <i class="pi pi-question-circle field-info-icon" 
               pTooltip="Select which events trigger notifications"></i>
            Notification Events
          </label>
          <div class="grid">
            <div class="col-6">
              <div class="field-checkbox">
                <p-checkbox formControlName="onFailedImportStrike" [binary]="true" inputId="on-failed-import-strike"></p-checkbox>
                <label for="on-failed-import-strike" class="ml-2">On Failed Import Strike</label>
              </div>
            </div>
            <div class="col-6">
              <div class="field-checkbox">
                <p-checkbox formControlName="onStalledStrike" [binary]="true" inputId="on-stalled-strike"></p-checkbox>
                <label for="on-stalled-strike" class="ml-2">On Stalled Strike</label>
              </div>
            </div>
            <div class="col-6">
              <div class="field-checkbox">
                <p-checkbox formControlName="onSlowStrike" [binary]="true" inputId="on-slow-strike"></p-checkbox>
                <label for="on-slow-strike" class="ml-2">On Slow Strike</label>
              </div>
            </div>
            <div class="col-6">
              <div class="field-checkbox">
                <p-checkbox formControlName="onQueueItemDeleted" [binary]="true" inputId="on-queue-item-deleted"></p-checkbox>
                <label for="on-queue-item-deleted" class="ml-2">On Queue Item Deleted</label>
              </div>
            </div>
            <div class="col-6">
              <div class="field-checkbox">
                <p-checkbox formControlName="onDownloadCleaned" [binary]="true" inputId="on-download-cleaned"></p-checkbox>
                <label for="on-download-cleaned" class="ml-2">On Download Cleaned</label>
              </div>
            </div>
            <div class="col-6">
              <div class="field-checkbox">
                <p-checkbox formControlName="onCategoryChanged" [binary]="true" inputId="on-category-changed"></p-checkbox>
                <label for="on-category-changed" class="ml-2">On Category Changed</label>
              </div>
            </div>
          </div>
        </div>
      </form>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end">
          <button 
            pButton 
            type="button" 
            label="Cancel" 
            icon="pi pi-times"
            class="p-button-secondary"
            (click)="onCancel()">
          </button>
          <button 
            pButton 
            type="button" 
            label="Test" 
            icon="pi pi-play"
            class="p-button-outlined ml-2"
            [disabled]="providerForm.invalid || testing"
            [loading]="testing"
            (click)="onTest()">
          </button>
          <button 
            pButton 
            type="button" 
            label="Save" 
            icon="pi pi-save"
            class="p-button-primary ml-2"
            [disabled]="providerForm.invalid || saving"
            [loading]="saving"
            (click)="onSave()">
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styleUrls: ['./apprise-provider.component.scss']
})
export class AppriseProviderComponent implements OnInit, OnDestroy {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<AppriseFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<AppriseFormData>();

  protected readonly formBuilder = inject(FormBuilder);

  // URL pattern for validation
  private readonly urlPattern = /^https?:\/\/(?:[-\w.])+(?::[0-9]+)?(?:\/.*)?$/;

  providerForm: FormGroup = this.formBuilder.group({
    // Base fields
    name: ['', [Validators.required, Validators.minLength(2)]],
    enabled: [true],
    
    // Event fields
    onFailedImportStrike: [false],
    onStalledStrike: [false],
    onSlowStrike: [false],
    onQueueItemDeleted: [false],
    onDownloadCleaned: [false],
    onCategoryChanged: [false],
    
    // Apprise-specific fields
    fullUrl: ['', [Validators.required, Validators.pattern(this.urlPattern)]],
    key: ['', [Validators.required, Validators.minLength(2)]],
    tags: [''] // Optional field
  });

  ngOnInit(): void {
    if (this.editingProvider) {
      this.populateForm();
    }
  }

  ngOnDestroy(): void {
    // Component cleanup if needed
  }

  private populateForm(): void {
    if (this.editingProvider) {
      const config = this.editingProvider.configuration as any;
      this.providerForm.patchValue({
        name: this.editingProvider.name,
        enabled: this.editingProvider.isEnabled,
        onFailedImportStrike: this.editingProvider.events.onFailedImportStrike,
        onStalledStrike: this.editingProvider.events.onStalledStrike,
        onSlowStrike: this.editingProvider.events.onSlowStrike,
        onQueueItemDeleted: this.editingProvider.events.onQueueItemDeleted,
        onDownloadCleaned: this.editingProvider.events.onDownloadCleaned,
        onCategoryChanged: this.editingProvider.events.onCategoryChanged,
        fullUrl: config?.url || config?.fullUrl || '',
        key: config?.key || '',
        tags: config?.tags || ''
      });
    }
  }

  protected hasError(fieldName: string, errorType: string): boolean {
    const field = this.providerForm.get(fieldName);
    return !!(field && field.errors?.[errorType] && (field.dirty || field.touched));
  }

  onSave(): void {
    if (this.providerForm.valid) {
      const formValue = this.providerForm.value;
      const appriseData: AppriseFormData = {
        name: formValue.name,
        enabled: formValue.enabled,
        onFailedImportStrike: formValue.onFailedImportStrike,
        onStalledStrike: formValue.onStalledStrike,
        onSlowStrike: formValue.onSlowStrike,
        onQueueItemDeleted: formValue.onQueueItemDeleted,
        onDownloadCleaned: formValue.onDownloadCleaned,
        onCategoryChanged: formValue.onCategoryChanged,
        fullUrl: formValue.fullUrl,
        key: formValue.key,
        tags: formValue.tags || ''
      };
      this.save.emit(appriseData);
    } else {
      // Mark all fields as touched to show validation errors
      Object.keys(this.providerForm.controls).forEach(key => {
        this.providerForm.get(key)?.markAsTouched();
      });
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(): void {
    if (this.providerForm.valid) {
      const formValue = this.providerForm.value;
      const appriseData: AppriseFormData = {
        name: formValue.name,
        enabled: formValue.enabled,
        onFailedImportStrike: formValue.onFailedImportStrike,
        onStalledStrike: formValue.onStalledStrike,
        onSlowStrike: formValue.onSlowStrike,
        onQueueItemDeleted: formValue.onQueueItemDeleted,
        onDownloadCleaned: formValue.onDownloadCleaned,
        onCategoryChanged: formValue.onCategoryChanged,
        fullUrl: formValue.fullUrl,
        key: formValue.key,
        tags: formValue.tags || ''
      };
      this.test.emit(appriseData);
    } else {
      // Mark all fields as touched to show validation errors
      Object.keys(this.providerForm.controls).forEach(key => {
        this.providerForm.get(key)?.markAsTouched();
      });
    }
  }
}
