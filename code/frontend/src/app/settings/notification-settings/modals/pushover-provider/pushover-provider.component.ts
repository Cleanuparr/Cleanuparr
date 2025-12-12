import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { MobileAutocompleteComponent } from '../../../../shared/components/mobile-autocomplete/mobile-autocomplete.component';
import { PushoverFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';
import { PushoverPriority } from '../../../../shared/models/pushover-priority.enum';
import { PushoverSounds } from '../../../../shared/models/pushover-sounds';

@Component({
  selector: 'app-pushover-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    InputNumberModule,
    SelectModule,
    MobileAutocompleteComponent,
    NotificationProviderBaseComponent
  ],
  templateUrl: './pushover-provider.component.html',
  styleUrls: ['./pushover-provider.component.scss']
})
export class PushoverProviderComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<PushoverFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<PushoverFormData>();

  // Provider-specific form controls
  apiTokenControl = new FormControl('', [Validators.required]);
  userKeyControl = new FormControl('', [Validators.required]);
  devicesControl = new FormControl<string[]>([]);
  priorityControl = new FormControl(PushoverPriority.Normal, [Validators.required]);
  soundControl = new FormControl('');
  customSoundControl = new FormControl('');
  retryControl = new FormControl<number | null>(null);
  expireControl = new FormControl<number | null>(null);
  tagsControl = new FormControl<string[]>([]);

  private documentationService = inject(DocumentationService);

  // Enum reference for template
  readonly PushoverPriority = PushoverPriority;

  // Priority dropdown options
  priorityOptions = [
    { label: 'Lowest (-2) - No notification', value: PushoverPriority.Lowest },
    { label: 'Low (-1) - No sound/vibration', value: PushoverPriority.Low },
    { label: 'Normal (0) - Default', value: PushoverPriority.Normal },
    { label: 'High (1) - Bypass quiet hours', value: PushoverPriority.High },
    { label: 'Emergency (2) - Repeat until acknowledged', value: PushoverPriority.Emergency }
  ];

  // Sound dropdown options - built-in sounds + custom option
  soundOptions = [
    { label: '(Use default)', value: '' },
    ...PushoverSounds.map(s => ({ label: s.label, value: s.value })),
    { label: 'Custom...', value: '__custom__' }
  ];

  // Track if custom sound is selected
  isCustomSound = false;

  /**
   * Exposed for template to open documentation for pushover fields
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications/pushover', fieldName);
  }

  ngOnInit(): void {
    // Set up conditional validation for emergency priority fields
    this.priorityControl.valueChanges.subscribe(priority => {
      this.updateEmergencyFieldValidation(priority);
    });

    // Track custom sound selection
    this.soundControl.valueChanges.subscribe(value => {
      this.isCustomSound = value === '__custom__';
      if (!this.isCustomSound) {
        this.customSoundControl.setValue('');
      }
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

      this.apiTokenControl.setValue(config?.apiToken || '');
      this.userKeyControl.setValue(config?.userKey || '');
      this.devicesControl.setValue(config?.devices || []);
      this.priorityControl.setValue(config?.priority || PushoverPriority.Normal);

      // Handle sound - check if it's a built-in sound or custom
      const savedSound = config?.sound || '';
      const isBuiltIn = PushoverSounds.some(s => s.value === savedSound) || savedSound === '';
      if (isBuiltIn) {
        this.soundControl.setValue(savedSound);
        this.customSoundControl.setValue('');
        this.isCustomSound = false;
      } else {
        this.soundControl.setValue('__custom__');
        this.customSoundControl.setValue(savedSound);
        this.isCustomSound = true;
      }

      this.retryControl.setValue(config?.retry || null);
      this.expireControl.setValue(config?.expire || null);
      this.tagsControl.setValue(config?.tags || []);

      // Update validation based on loaded priority
      this.updateEmergencyFieldValidation(config?.priority || PushoverPriority.Normal);
    }
  }

  private resetProviderFields(): void {
    this.apiTokenControl.setValue('');
    this.userKeyControl.setValue('');
    this.devicesControl.setValue([]);
    this.priorityControl.setValue(PushoverPriority.Normal);
    this.soundControl.setValue('');
    this.customSoundControl.setValue('');
    this.isCustomSound = false;
    this.retryControl.setValue(null);
    this.expireControl.setValue(null);
    this.tagsControl.setValue([]);

    // Reset validation
    this.updateEmergencyFieldValidation(PushoverPriority.Normal);
  }

  private updateEmergencyFieldValidation(priority: PushoverPriority | null): void {
    this.retryControl.clearValidators();
    this.expireControl.clearValidators();

    if (priority === PushoverPriority.Emergency) {
      this.retryControl.setValidators([Validators.required, Validators.min(30)]);
      this.expireControl.setValidators([Validators.required, Validators.min(1), Validators.max(10800)]);
    }

    this.retryControl.updateValueAndValidity();
    this.expireControl.updateValueAndValidity();
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  private isFormValid(): boolean {
    const baseValid = this.apiTokenControl.valid &&
                      this.userKeyControl.valid &&
                      this.priorityControl.valid;

    if (this.currentPriority === PushoverPriority.Emergency) {
      return baseValid && this.retryControl.valid && this.expireControl.valid;
    }

    return baseValid;
  }

  private getEffectiveSound(): string {
    if (this.isCustomSound) {
      return this.customSoundControl.value || '';
    }
    return this.soundControl.value || '';
  }

  private buildPushoverData(baseData: BaseProviderFormData): PushoverFormData {
    return {
      ...baseData,
      apiToken: this.apiTokenControl.value || '',
      userKey: this.userKeyControl.value || '',
      devices: this.devicesControl.value || [],
      priority: this.priorityControl.value || PushoverPriority.Normal,
      sound: this.getEffectiveSound(),
      retry: this.currentPriority === PushoverPriority.Emergency ? this.retryControl.value : null,
      expire: this.currentPriority === PushoverPriority.Emergency ? this.expireControl.value : null,
      tags: this.tagsControl.value || []
    };
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      const pushoverData = this.buildPushoverData(baseData);
      this.save.emit(pushoverData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.apiTokenControl.markAsTouched();
      this.userKeyControl.markAsTouched();
      this.priorityControl.markAsTouched();
      this.retryControl.markAsTouched();
      this.expireControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      const pushoverData = this.buildPushoverData(baseData);
      this.test.emit(pushoverData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.apiTokenControl.markAsTouched();
      this.userKeyControl.markAsTouched();
      this.priorityControl.markAsTouched();
      this.retryControl.markAsTouched();
      this.expireControl.markAsTouched();
    }
  }

  /**
   * Get current priority for template conditionals
   */
  get currentPriority(): PushoverPriority | null {
    return this.priorityControl.value;
  }
}
