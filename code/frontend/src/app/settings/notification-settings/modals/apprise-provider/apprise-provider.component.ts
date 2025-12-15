import { Component, Input, Output, EventEmitter, OnInit, OnChanges, OnDestroy, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { MobileAutocompleteComponent } from '../../../../shared/components/mobile-autocomplete/mobile-autocomplete.component';
import { SelectModule } from 'primeng/select';
import { Message } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { AppriseFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { NotificationProviderService } from '../../../../core/services/notification-provider.service';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';
import { UrlValidators } from '../../../../core/validators/url.validator';
import { AppriseMode } from '../../../../shared/models/enums';

@Component({
  selector: 'app-apprise-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    SelectModule,
    Message,
    ProgressSpinnerModule,
    MobileAutocompleteComponent,
    NotificationProviderBaseComponent
  ],
  templateUrl: './apprise-provider.component.html',
  styleUrls: ['./apprise-provider.component.scss']
})
export class AppriseProviderComponent implements OnInit, OnChanges, OnDestroy {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<AppriseFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<AppriseFormData>();

  private documentationService = inject(DocumentationService);
  private notificationProviderService = inject(NotificationProviderService);

  // Mode selection
  modeControl = new FormControl<AppriseMode>(AppriseMode.Api, { nonNullable: true });
  modeOptions = [
    { label: 'API', value: AppriseMode.Api },
    { label: 'CLI', value: AppriseMode.Cli }
  ];

  // CLI availability status
  checkingCliAvailability = false;
  cliAvailable = false;
  cliVersion: string | null = null;

  // API mode form controls
  urlControl = new FormControl('', [Validators.required, UrlValidators.httpUrl]);
  keyControl = new FormControl('', [Validators.required, Validators.minLength(2)]);
  tagsControl = new FormControl(''); // Optional field

  // CLI mode form controls
  serviceUrlsControl = new FormControl<string[]>([]);

  // Subscription for mode changes
  private modeSubscription?: Subscription;
  private cliCheckedThisSession = false;

  /**
   * Exposed for template to open documentation for apprise fields
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications/apprise', fieldName);
  }

  ngOnInit(): void {
    // Subscribe to mode changes to check CLI availability when switching to CLI mode
    this.modeSubscription = this.modeControl.valueChanges.subscribe((mode) => {
      if (mode === AppriseMode.Cli && !this.cliCheckedThisSession) {
        this.checkCliAvailability();
      }
    });
  }

  ngOnDestroy(): void {
    this.modeSubscription?.unsubscribe();
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

    // When modal becomes visible, reset the CLI check flag and check if already in CLI mode
    if (changes['visible'] && this.visible) {
      this.cliCheckedThisSession = false;
      // Only check CLI availability if mode is already CLI (editing existing CLI provider)
      if (this.modeControl.value === AppriseMode.Cli) {
        this.checkCliAvailability();
      }
    }
  }

  private checkCliAvailability(): void {
    this.checkingCliAvailability = true;
    this.cliCheckedThisSession = true;
    this.notificationProviderService.getAppriseCliStatus().subscribe({
      next: (status) => {
        this.cliAvailable = status.available;
        this.cliVersion = status.version;
        this.checkingCliAvailability = false;
      },
      error: () => {
        this.cliAvailable = false;
        this.cliVersion = null;
        this.checkingCliAvailability = false;
      }
    });
  }

  private populateProviderFields(): void {
    if (this.editingProvider) {
      const config = this.editingProvider.configuration as any;

      this.modeControl.setValue(config?.mode || AppriseMode.Api);
      // API mode fields
      this.urlControl.setValue(config?.url || '');
      this.keyControl.setValue(config?.key || '');
      this.tagsControl.setValue(config?.tags || '');
      // CLI mode fields - convert newline-separated string to array
      const serviceUrlsString = config?.serviceUrls || '';
      const serviceUrlsArray = serviceUrlsString
        .split('\n')
        .map((url: string) => url.trim())
        .filter((url: string) => url.length > 0);
      this.serviceUrlsControl.setValue(serviceUrlsArray);
    }
  }

  private resetProviderFields(): void {
    this.modeControl.setValue(AppriseMode.Api);
    this.urlControl.setValue('');
    this.keyControl.setValue('');
    this.tagsControl.setValue('');
    this.serviceUrlsControl.setValue([]);
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  private isFormValid(): boolean {
    const mode = this.modeControl.value;
    if (mode === AppriseMode.Api) {
      return this.urlControl.valid && this.keyControl.valid;
    } else {
      // CLI mode requires at least one service URL
      const serviceUrls = this.serviceUrlsControl.value || [];
      return serviceUrls.length > 0;
    }
  }

  private markFieldsTouched(): void {
    const mode = this.modeControl.value;
    if (mode === AppriseMode.Api) {
      this.urlControl.markAsTouched();
      this.keyControl.markAsTouched();
    } else {
      this.serviceUrlsControl.markAsTouched();
    }
  }

  private buildFormData(baseData: BaseProviderFormData): AppriseFormData {
    // Convert array to newline-separated string for backend
    const serviceUrlsArray = this.serviceUrlsControl.value || [];
    const serviceUrlsString = serviceUrlsArray.join('\n');

    return {
      ...baseData,
      mode: this.modeControl.value,
      url: this.urlControl.value || '',
      key: this.keyControl.value || '',
      tags: this.tagsControl.value || '',
      serviceUrls: serviceUrlsString
    };
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      this.save.emit(this.buildFormData(baseData));
    } else {
      this.markFieldsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      this.test.emit(this.buildFormData(baseData));
    } else {
      this.markFieldsTouched();
    }
  }
}
