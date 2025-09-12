import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { SelectModule } from 'primeng/select';
import { MobileAutocompleteComponent } from '../../../../shared/components/mobile-autocomplete/mobile-autocomplete.component';
import { NtfyFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';
import { UrlValidators } from '../../../../core/validators/url.validator';
import { NtfyAuthenticationType } from '../../../../shared/models/ntfy-authentication-type.enum';
import { NtfyPriority } from '../../../../shared/models/ntfy-priority.enum';

@Component({
  selector: 'app-ntfy-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    AutoCompleteModule,
    SelectModule,
    MobileAutocompleteComponent,
    NotificationProviderBaseComponent
  ],
  templateUrl: './ntfy-provider.component.html',
  styleUrls: ['./ntfy-provider.component.scss']
})
export class NtfyProviderComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<NtfyFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<NtfyFormData>();

  // Provider-specific form controls
  serverUrlControl = new FormControl('', [Validators.required, UrlValidators.httpUrl]);
  topicsControl = new FormControl<string[]>([], [Validators.required, Validators.minLength(1)]);
  authenticationTypeControl = new FormControl(NtfyAuthenticationType.None, [Validators.required]);
  usernameControl = new FormControl('');
  passwordControl = new FormControl('');
  accessTokenControl = new FormControl('');
  priorityControl = new FormControl(NtfyPriority.Default, [Validators.required]);
  tagsControl = new FormControl<string[]>([]);

  private documentationService = inject(DocumentationService);

  // Enum references for template
  readonly NtfyAuthenticationType = NtfyAuthenticationType;
  readonly NtfyPriority = NtfyPriority;

  // Dropdown options
  authenticationOptions = [
    { label: 'None', value: NtfyAuthenticationType.None },
    { label: 'Basic Auth', value: NtfyAuthenticationType.BasicAuth },
    { label: 'Access Token', value: NtfyAuthenticationType.AccessToken }
  ];

  priorityOptions = [
    { label: 'Min', value: NtfyPriority.Min },
    { label: 'Low', value: NtfyPriority.Low },
    { label: 'Default', value: NtfyPriority.Default },
    { label: 'High', value: NtfyPriority.High },
    { label: 'Max', value: NtfyPriority.Max }
  ];

  /**
   * Exposed for template to open documentation for ntfy fields
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications/ntfy', fieldName);
  }

  ngOnInit(): void {
    // Initialize component but don't populate yet - wait for ngOnChanges
    
    // Set up conditional validation for authentication fields
    this.authenticationTypeControl.valueChanges.subscribe(type => {
      this.updateAuthFieldValidation(type);
    });
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
      
      this.serverUrlControl.setValue(config?.serverUrl || 'https://ntfy.sh');
      this.topicsControl.setValue(config?.topics || []);
      this.authenticationTypeControl.setValue(config?.authenticationType || NtfyAuthenticationType.None);
      this.usernameControl.setValue(config?.username || '');
      this.passwordControl.setValue(config?.password || '');
      this.accessTokenControl.setValue(config?.accessToken || '');
      this.priorityControl.setValue(config?.priority || NtfyPriority.Default);
      this.tagsControl.setValue(config?.tags || []);
    }
  }

  private resetProviderFields(): void {
    this.serverUrlControl.setValue('https://ntfy.sh');
    this.topicsControl.setValue([]);
    this.authenticationTypeControl.setValue(NtfyAuthenticationType.None);
    this.usernameControl.setValue('');
    this.passwordControl.setValue('');
    this.accessTokenControl.setValue('');
    this.priorityControl.setValue(NtfyPriority.Default);
    this.tagsControl.setValue([]);
  }

  private updateAuthFieldValidation(authType: NtfyAuthenticationType | null): void {
    // Clear previous validators
    this.usernameControl.clearValidators();
    this.passwordControl.clearValidators();
    this.accessTokenControl.clearValidators();

    // Set validators based on auth type
    if (authType === NtfyAuthenticationType.BasicAuth) {
      this.usernameControl.setValidators([Validators.required]);
      this.passwordControl.setValidators([Validators.required]);
    } else if (authType === NtfyAuthenticationType.AccessToken) {
      this.accessTokenControl.setValidators([Validators.required]);
    }

    // Update validation status
    this.usernameControl.updateValueAndValidity();
    this.passwordControl.updateValueAndValidity();
    this.accessTokenControl.updateValueAndValidity();
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  private isFormValid(): boolean {
    return this.serverUrlControl.valid && 
           this.topicsControl.valid && 
           this.authenticationTypeControl.valid &&
           this.usernameControl.valid &&
           this.passwordControl.valid &&
           this.accessTokenControl.valid &&
           this.priorityControl.valid;
  }

  private buildNtfyData(baseData: BaseProviderFormData): NtfyFormData {
    return {
      ...baseData,
      serverUrl: this.serverUrlControl.value || '',
      topics: this.topicsControl.value || [],
      authenticationType: this.authenticationTypeControl.value || NtfyAuthenticationType.None,
      username: this.usernameControl.value || '',
      password: this.passwordControl.value || '',
      accessToken: this.accessTokenControl.value || '',
      priority: this.priorityControl.value || NtfyPriority.Default,
      tags: this.tagsControl.value || []
    };
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      const ntfyData = this.buildNtfyData(baseData);
      this.save.emit(ntfyData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.serverUrlControl.markAsTouched();
      this.topicsControl.markAsTouched();
      this.authenticationTypeControl.markAsTouched();
      this.usernameControl.markAsTouched();
      this.passwordControl.markAsTouched();
      this.accessTokenControl.markAsTouched();
      this.priorityControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      const ntfyData = this.buildNtfyData(baseData);
      this.test.emit(ntfyData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.serverUrlControl.markAsTouched();
      this.topicsControl.markAsTouched();
      this.authenticationTypeControl.markAsTouched();
      this.usernameControl.markAsTouched();
      this.passwordControl.markAsTouched();
      this.accessTokenControl.markAsTouched();
      this.priorityControl.markAsTouched();
    }
  }

  /**
   * Get current authentication type for template conditionals
   */
  get currentAuthType(): NtfyAuthenticationType | null {
    return this.authenticationTypeControl.value;
  }
}
