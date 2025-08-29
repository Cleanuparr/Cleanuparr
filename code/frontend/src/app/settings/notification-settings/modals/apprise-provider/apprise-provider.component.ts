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
  templateUrl: './apprise-provider.component.html',
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
