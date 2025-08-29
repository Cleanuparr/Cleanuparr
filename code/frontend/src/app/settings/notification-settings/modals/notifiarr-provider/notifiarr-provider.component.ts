import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { NotifiarrFormData, ProviderModalConfig } from '../../models/provider-modal.model';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';

@Component({
  selector: 'app-notifiarr-provider',
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
  templateUrl: './notifiarr-provider.component.html',
  styleUrls: ['./notifiarr-provider.component.scss']
})
export class NotifiarrProviderComponent implements OnInit, OnDestroy {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<NotifiarrFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<NotifiarrFormData>();

  protected readonly formBuilder = inject(FormBuilder);

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
    
    // Notifiarr-specific fields
    apiKey: ['', [Validators.required, Validators.minLength(10)]],
    channelId: ['', [Validators.required, Validators.pattern(/^\d+$/)]]
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
        apiKey: config?.apiKey || '',
        channelId: config?.channelId || ''
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
      const notifiarrData: NotifiarrFormData = {
        name: formValue.name,
        enabled: formValue.enabled,
        onFailedImportStrike: formValue.onFailedImportStrike,
        onStalledStrike: formValue.onStalledStrike,
        onSlowStrike: formValue.onSlowStrike,
        onQueueItemDeleted: formValue.onQueueItemDeleted,
        onDownloadCleaned: formValue.onDownloadCleaned,
        onCategoryChanged: formValue.onCategoryChanged,
        apiKey: formValue.apiKey,
        channelId: formValue.channelId
      };
      this.save.emit(notifiarrData);
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
      const notifiarrData: NotifiarrFormData = {
        name: formValue.name,
        enabled: formValue.enabled,
        onFailedImportStrike: formValue.onFailedImportStrike,
        onStalledStrike: formValue.onStalledStrike,
        onSlowStrike: formValue.onSlowStrike,
        onQueueItemDeleted: formValue.onQueueItemDeleted,
        onDownloadCleaned: formValue.onDownloadCleaned,
        onCategoryChanged: formValue.onCategoryChanged,
        apiKey: formValue.apiKey,
        channelId: formValue.channelId
      };
      this.test.emit(notifiarrData);
    } else {
      // Mark all fields as touched to show validation errors
      Object.keys(this.providerForm.controls).forEach(key => {
        this.providerForm.get(key)?.markAsTouched();
      });
    }
  }
}
