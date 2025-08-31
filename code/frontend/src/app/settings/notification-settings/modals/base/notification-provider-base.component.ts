import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
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
  ],
  templateUrl: './notification-provider-base.component.html',
  styleUrls: ['./notification-provider-base.component.scss']
})
export class NotificationProviderBaseComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() modalTitle = 'Configure Notification Provider';
  @Input() saving = false;
  @Input() testing = false;
  @Input() editingProvider: NotificationProviderDto | null = null;

  @Output() save = new EventEmitter<BaseProviderFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<BaseProviderFormData>();

  protected readonly formBuilder = inject(FormBuilder);
  private readonly documentationService = inject(DocumentationService);

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
    // Initialize form but don't populate yet - wait for ngOnChanges
  }

  ngOnChanges(changes: SimpleChanges) {
    // Populate form when editingProvider input changes
    if (changes['editingProvider']) {
      if (this.editingProvider) {
        this.populateForm();
      } else {
        // Reset form when editingProvider is cleared
        this.resetForm();
      }
    }
  }

  protected populateForm() {
    if (this.editingProvider) {
      this.baseForm.patchValue({
        name: this.editingProvider.name,
        enabled: this.editingProvider.isEnabled,
        onFailedImportStrike: this.editingProvider.events?.onFailedImportStrike || false,
        onStalledStrike: this.editingProvider.events?.onStalledStrike || false,
        onSlowStrike: this.editingProvider.events?.onSlowStrike || false,
        onQueueItemDeleted: this.editingProvider.events?.onQueueItemDeleted || false,
        onDownloadCleaned: this.editingProvider.events?.onDownloadCleaned || false,
        onCategoryChanged: this.editingProvider.events?.onCategoryChanged || false
      });
    }
  }

  protected resetForm() {
    this.baseForm.reset({
      name: '',
      enabled: true,
      onFailedImportStrike: false,
      onStalledStrike: false,
      onSlowStrike: false,
      onQueueItemDeleted: false,
      onDownloadCleaned: false,
      onCategoryChanged: false
    });
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

  /**
   * Open notifications documentation for a specific field (or the section when no field provided)
   */
  openFieldDocs(fieldName?: string): void {
    // pass empty string when undefined so the service falls back to section doc
    this.documentationService.openFieldDocumentation('notifications', fieldName ?? '');
  }
}
