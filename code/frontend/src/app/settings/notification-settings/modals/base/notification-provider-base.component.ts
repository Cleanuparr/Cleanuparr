import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { BaseProviderFormData } from '../../models/provider-modal.model';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';

@Component({
  selector: 'app-notification-provider-base',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    TooltipModule
  ],
  templateUrl: './notification-provider-base.component.html',
  styleUrls: ['./notification-provider-base.component.scss']
})
export class NotificationProviderBaseComponent implements OnInit {
  @Input() visible = false;
  @Input() modalTitle = 'Configure Notification Provider';
  @Input() saving = false;
  @Input() testing = false;
  @Input() editingProvider: NotificationProviderDto | null = null;

  @Output() save = new EventEmitter<BaseProviderFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<BaseProviderFormData>();

  protected readonly formBuilder = inject(FormBuilder);

  baseForm: FormGroup = this.formBuilder.group({
    name: ['', Validators.required],
    enabled: [true],
    onFailedImportStrike: [false],
    onStalledStrike: [false],
    onSlowStrike: [false],
    onQueueItemDeleted: [false],
    onDownloadCleaned: [false],
    onCategoryChanged: [false]
  });

  ngOnInit() {
    if (this.editingProvider) {
      this.populateForm();
    }
  }

  protected populateForm() {
    if (this.editingProvider) {
      this.baseForm.patchValue({
        name: this.editingProvider.name,
        enabled: this.editingProvider.isEnabled,
        onFailedImportStrike: this.editingProvider.events.onFailedImportStrike,
        onStalledStrike: this.editingProvider.events.onStalledStrike,
        onSlowStrike: this.editingProvider.events.onSlowStrike,
        onQueueItemDeleted: this.editingProvider.events.onQueueItemDeleted,
        onDownloadCleaned: this.editingProvider.events.onDownloadCleaned,
        onCategoryChanged: this.editingProvider.events.onCategoryChanged
      });
    }
  }

  protected hasError(fieldName: string, errorType: string): boolean {
    const field = this.baseForm.get(fieldName);
    return !!(field && field.errors?.[errorType] && (field.dirty || field.touched));
  }

  onSave() {
    if (this.baseForm.valid) {
      this.save.emit(this.baseForm.value as BaseProviderFormData);
    }
  }

  onCancel() {
    this.cancel.emit();
  }

  onTest() {
    if (this.baseForm.valid) {
      this.test.emit(this.baseForm.value as BaseProviderFormData);
    }
  }
}
