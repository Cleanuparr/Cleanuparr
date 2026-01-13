import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { GotifyFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';

@Component({
  selector: 'app-gotify-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    InputNumberModule,
    NotificationProviderBaseComponent
  ],
  templateUrl: './gotify-provider.component.html',
  styleUrls: ['./gotify-provider.component.scss']
})
export class GotifyProviderComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<GotifyFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<GotifyFormData>();

  // Provider-specific form controls
  serverUrlControl = new FormControl('', [Validators.required, Validators.pattern(/^https?:\/\/.+/)]);
  applicationTokenControl = new FormControl('', [Validators.required]);
  priorityControl = new FormControl(5, [Validators.required, Validators.min(0), Validators.max(10)]);

  private documentationService = inject(DocumentationService);

  /** Exposed for template to open documentation for gotify fields */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications/gotify', fieldName);
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

      this.serverUrlControl.setValue(config?.serverUrl || '');
      this.applicationTokenControl.setValue(config?.applicationToken || '');
      this.priorityControl.setValue(config?.priority ?? 5);
    }
  }

  private resetProviderFields(): void {
    this.serverUrlControl.setValue('');
    this.applicationTokenControl.setValue('');
    this.priorityControl.setValue(5);
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.serverUrlControl.valid && this.applicationTokenControl.valid && this.priorityControl.valid) {
      const gotifyData: GotifyFormData = {
        ...baseData,
        serverUrl: this.serverUrlControl.value || '',
        applicationToken: this.applicationTokenControl.value || '',
        priority: this.priorityControl.value ?? 5
      };
      this.save.emit(gotifyData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.serverUrlControl.markAsTouched();
      this.applicationTokenControl.markAsTouched();
      this.priorityControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.serverUrlControl.valid && this.applicationTokenControl.valid && this.priorityControl.valid) {
      const gotifyData: GotifyFormData = {
        ...baseData,
        serverUrl: this.serverUrlControl.value || '',
        applicationToken: this.applicationTokenControl.value || '',
        priority: this.priorityControl.value ?? 5
      };
      this.test.emit(gotifyData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.serverUrlControl.markAsTouched();
      this.applicationTokenControl.markAsTouched();
      this.priorityControl.markAsTouched();
    }
  }
}
