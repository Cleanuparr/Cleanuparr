import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { DiscordFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { DocumentationService } from '../../../../core/services/documentation.service';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';

@Component({
  selector: 'app-discord-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    NotificationProviderBaseComponent
  ],
  templateUrl: './discord-provider.component.html',
  styleUrls: ['./discord-provider.component.scss']
})
export class DiscordProviderComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<DiscordFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<DiscordFormData>();

  // Provider-specific form controls
  webhookUrlControl = new FormControl('', [Validators.required, Validators.pattern(/^https:\/\/(discord\.com|discordapp\.com)\/api\/webhooks\/.+/)]);
  usernameControl = new FormControl('', [Validators.maxLength(80)]);
  avatarUrlControl = new FormControl('', [Validators.pattern(/^(https?:\/\/.+)?$/)]);

  private documentationService = inject(DocumentationService);

  /** Exposed for template to open documentation for discord fields */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications/discord', fieldName);
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

      this.webhookUrlControl.setValue(config?.webhookUrl || '');
      this.usernameControl.setValue(config?.username || '');
      this.avatarUrlControl.setValue(config?.avatarUrl || '');
    }
  }

  private resetProviderFields(): void {
    this.webhookUrlControl.setValue('');
    this.usernameControl.setValue('');
    this.avatarUrlControl.setValue('https://github.com/Cleanuparr/Cleanuparr/blob/main/Logo/48.png?raw=true');
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.webhookUrlControl.valid && this.usernameControl.valid && this.avatarUrlControl.valid) {
      const discordData: DiscordFormData = {
        ...baseData,
        webhookUrl: this.webhookUrlControl.value || '',
        username: this.usernameControl.value || '',
        avatarUrl: this.avatarUrlControl.value || ''
      };
      this.save.emit(discordData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.webhookUrlControl.markAsTouched();
      this.usernameControl.markAsTouched();
      this.avatarUrlControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.webhookUrlControl.valid && this.usernameControl.valid && this.avatarUrlControl.valid) {
      const discordData: DiscordFormData = {
        ...baseData,
        webhookUrl: this.webhookUrlControl.value || '',
        username: this.usernameControl.value || '',
        avatarUrl: this.avatarUrlControl.value || ''
      };
      this.test.emit(discordData);
    } else {
      // Mark provider-specific fields as touched to show validation errors
      this.webhookUrlControl.markAsTouched();
      this.usernameControl.markAsTouched();
      this.avatarUrlControl.markAsTouched();
    }
  }
}
