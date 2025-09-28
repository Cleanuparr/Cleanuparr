import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { NotifiarrFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';
import { NumericInputDirective } from '../../../../shared/directives';

@Component({
  selector: 'app-notifiarr-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    NotificationProviderBaseComponent,
    NumericInputDirective
  ],
  templateUrl: './notifiarr-provider.component.html',
  styleUrls: ['./notifiarr-provider.component.scss']
})
export class NotifiarrProviderComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<NotifiarrFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<NotifiarrFormData>();

  // Provider-specific form controls
  apiKeyControl = new FormControl('', [Validators.required, Validators.minLength(10)]);
  channelIdControl = new FormControl('', [Validators.required]);
  private documentationService = inject(DocumentationService);

  /** Exposed for template to open documentation for notifiarr fields */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications/notifiarr', fieldName);
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
      
      this.apiKeyControl.setValue(config?.apiKey || '');
      this.channelIdControl.setValue(config?.channelId || '');
    }
  }

  private resetProviderFields(): void {
    this.apiKeyControl.setValue('');
    this.channelIdControl.setValue('');
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.apiKeyControl.valid && this.channelIdControl.valid) {
      const notifiarrData: NotifiarrFormData = {
        ...baseData,
        apiKey: this.apiKeyControl.value || '',
        channelId: this.channelIdControl.value || ''
      };
      this.save.emit(notifiarrData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.apiKeyControl.markAsTouched();
      this.channelIdControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.apiKeyControl.valid && this.channelIdControl.valid) {
      const notifiarrData: NotifiarrFormData = {
        ...baseData,
        apiKey: this.apiKeyControl.value || '',
        channelId: this.channelIdControl.value || ''
      };
      this.test.emit(notifiarrData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.apiKeyControl.markAsTouched();
      this.channelIdControl.markAsTouched();
    }
  }
}
