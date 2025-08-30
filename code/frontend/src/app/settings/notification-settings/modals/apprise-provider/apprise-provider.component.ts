import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { AppriseFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';

@Component({
  selector: 'app-apprise-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    TooltipModule,
    NotificationProviderBaseComponent
  ],
  templateUrl: './apprise-provider.component.html',
  styleUrls: ['./apprise-provider.component.scss']
})
export class AppriseProviderComponent implements OnInit, OnDestroy, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<AppriseFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<AppriseFormData>();

  // URL pattern for validation
  private readonly urlPattern = /^https?:\/\/(?:[-\w.])+(?::[0-9]+)?(?:\/.*)?$/;

  // Provider-specific form controls
  fullUrlControl = new FormControl('', [Validators.required, Validators.pattern(this.urlPattern)]);
  keyControl = new FormControl('', [Validators.required, Validators.minLength(2)]);
  tagsControl = new FormControl(''); // Optional field

  ngOnInit(): void {
    // Initialize component but don't populate yet - wait for ngOnChanges
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Populate provider-specific fields when editingProvider input changes
    if (changes['editingProvider']) {
      if (this.editingProvider) {
        this.populateProviderFields();
      } else {
        // Reset fields when editingProvider is cleared
        this.resetProviderFields();
      }
    }
  }

  ngOnDestroy(): void {
    // Component cleanup if needed
  }

  private populateProviderFields(): void {
    if (this.editingProvider) {
      console.log('Populating Apprise fields with provider:', this.editingProvider);
      const config = this.editingProvider.configuration as any;
      console.log('Provider configuration:', config);
      
      // Handle both 'url' and 'fullUrl' for backwards compatibility
      this.fullUrlControl.setValue(config?.url || config?.fullUrl || '');
      this.keyControl.setValue(config?.key || '');
      this.tagsControl.setValue(config?.tags || '');
      
      console.log('Apprise fields populated - URL:', this.fullUrlControl.value, 'Key:', this.keyControl.value, 'Tags:', this.tagsControl.value);
    }
  }

  private resetProviderFields(): void {
    this.fullUrlControl.setValue('');
    this.keyControl.setValue('');
    this.tagsControl.setValue('');
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.fullUrlControl.valid && this.keyControl.valid) {
      const appriseData: AppriseFormData = {
        ...baseData,
        fullUrl: this.fullUrlControl.value || '',
        key: this.keyControl.value || '',
        tags: this.tagsControl.value || ''
      };
      this.save.emit(appriseData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.fullUrlControl.markAsTouched();
      this.keyControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.fullUrlControl.valid && this.keyControl.valid) {
      const appriseData: AppriseFormData = {
        ...baseData,
        fullUrl: this.fullUrlControl.value || '',
        key: this.keyControl.value || '',
        tags: this.tagsControl.value || ''
      };
      this.test.emit(appriseData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.fullUrlControl.markAsTouched();
      this.keyControl.markAsTouched();
    }
  }
}
