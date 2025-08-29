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
  template: `
    <p-dialog 
      [(visible)]="visible" 
      [modal]="true" 
      [closable]="true"
      [draggable]="false"
      [resizable]="false"
      styleClass="instance-modal"
      [header]="modalTitle"
      (onHide)="onCancel()">
      
      <form [formGroup]="baseForm" class="p-fluid instance-form">
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
            placeholder="My Notification Provider"
            class="w-full" />
          <small *ngIf="hasError('name', 'required')" class="p-error">
            Provider name is required
          </small>
        </div>

        <!-- Provider-Specific Configuration (Content Projection) -->
        <ng-content select="[slot=provider-config]"></ng-content>

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

      <!-- Modal Footer -->
      <ng-template pTemplate="footer">
        <div class="modal-footer">
          <button 
            pButton 
            type="button" 
            label="Cancel" 
            class="p-button-text"
            (click)="onCancel()">
          </button>
          <button 
            pButton 
            type="button" 
            label="Test" 
            icon="pi pi-play"
            class="p-button-outlined ml-2"
            [disabled]="baseForm.invalid || testing"
            [loading]="testing"
            (click)="onTest()">
          </button>
          <button 
            pButton 
            type="button" 
            label="Save" 
            icon="pi pi-save"
            class="p-button-primary ml-2"
            [disabled]="baseForm.invalid || saving"
            [loading]="saving"
            (click)="onSave()">
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
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
