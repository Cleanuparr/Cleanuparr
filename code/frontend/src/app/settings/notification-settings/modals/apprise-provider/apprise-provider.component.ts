import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { AppriseFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';

@Component({
  selector: 'app-apprise-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    NotificationProviderBaseComponent
  ],
  templateUrl: './apprise-provider.component.html',
  styleUrls: ['./apprise-provider.component.scss']
})
export class AppriseProviderComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<AppriseFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<AppriseFormData>();

  // Provider-specific form controls
  urlControl = new FormControl('', [Validators.required, AppriseProviderComponent.url]);
  keyControl = new FormControl('', [Validators.required, Validators.minLength(2)]);
  tagsControl = new FormControl(''); // Optional field
  private documentationService = inject(DocumentationService);

  /**
   * Exposed for template to open documentation for apprise fields
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications', fieldName);
  }

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

  private populateProviderFields(): void {
    if (this.editingProvider) {
      const config = this.editingProvider.configuration as any;
      
      this.urlControl.setValue(config?.url || '');
      this.keyControl.setValue(config?.key || '');
      this.tagsControl.setValue(config?.tags || '');
    }
  }

  private resetProviderFields(): void {
    this.urlControl.setValue('');
    this.keyControl.setValue('');
    this.tagsControl.setValue('');
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.urlControl.valid && this.keyControl.valid) {
      const appriseData: AppriseFormData = {
        ...baseData,
        url: this.urlControl.value || '',
        key: this.keyControl.value || '',
        tags: this.tagsControl.value || ''
      };
      this.save.emit(appriseData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.urlControl.markAsTouched();
      this.keyControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.urlControl.valid && this.keyControl.valid) {
      const appriseData: AppriseFormData = {
        ...baseData,
        url: this.urlControl.value || '',
        key: this.keyControl.value || '',
        tags: this.tagsControl.value || ''
      };
      this.test.emit(appriseData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.urlControl.markAsTouched();
      this.keyControl.markAsTouched();
    }
  }

  public static url(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    
    // Return null if empty (let required validator handle empty case)
    if (!value || value === '') {
      return null;
    }
    
    try {
      // Accept only http or https protocols
      const url = new URL(value);
      if (url.protocol === 'http:' || url.protocol === 'https:') {
        return null; // Valid URL
      }
      return { url: true }; // Invalid protocol
    } catch {
      return { url: true }; // Invalid URL format
    }
  }
}
