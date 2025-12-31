import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { NotificationProviderBaseComponent } from '../base/notification-provider-base.component';
import { NumericInputDirective } from '../../../../shared/directives';
import { TelegramFormData, BaseProviderFormData } from '../../models/provider-modal.model';
import { NotificationProviderDto } from '../../../../shared/models/notification-provider.model';
import { DocumentationService } from '../../../../core/services/documentation.service';

@Component({
  selector: 'app-telegram-provider',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    CheckboxModule,
    NumericInputDirective,
    NotificationProviderBaseComponent
  ],
  templateUrl: './telegram-provider.component.html',
  styleUrls: ['./telegram-provider.component.scss']
})
export class TelegramProviderComponent implements OnInit, OnChanges {
  @Input() visible = false;
  @Input() editingProvider: NotificationProviderDto | null = null;
  @Input() saving = false;
  @Input() testing = false;

  @Output() save = new EventEmitter<TelegramFormData>();
  @Output() cancel = new EventEmitter<void>();
  @Output() test = new EventEmitter<TelegramFormData>();

  botTokenControl = new FormControl('', [Validators.required, Validators.minLength(10)]);
  chatIdControl = new FormControl('', [Validators.required]);
  topicIdControl = new FormControl('');
  sendSilentlyControl = new FormControl(false);

  private documentationService = inject(DocumentationService);

  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('notifications/telegram', fieldName);
  }

  ngOnInit(): void {
    // initialization handled in ngOnChanges
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['editingProvider']) {
      if (this.editingProvider) {
        this.populateProviderFields();
      } else {
        this.resetProviderFields();
      }
    }
  }

  private populateProviderFields(): void {
    if (!this.editingProvider) return;

    const config = this.editingProvider.configuration as any;
    this.botTokenControl.setValue(config?.botToken || '');
    this.chatIdControl.setValue(config?.chatId || '');
    this.topicIdControl.setValue(config?.topicId || '');
    this.sendSilentlyControl.setValue(!!config?.sendSilently);
  }

  private resetProviderFields(): void {
    this.botTokenControl.setValue('');
    this.chatIdControl.setValue('');
    this.topicIdControl.setValue('');
    this.sendSilentlyControl.setValue(false);
  }

  protected hasFieldError(control: FormControl, errorType: string): boolean {
    return !!(control && control.errors?.[errorType] && (control.dirty || control.touched));
  }

  private isFormValid(): boolean {
    return this.botTokenControl.valid && this.chatIdControl.valid;
  }

  onSave(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      const telegramData: TelegramFormData = {
        ...baseData,
        botToken: this.botTokenControl.value || '',
        chatId: this.chatIdControl.value || '',
        topicId: this.topicIdControl.value || '',
        sendSilently: this.sendSilentlyControl.value || false,
      };
      this.save.emit(telegramData);
    } else {
      this.botTokenControl.markAsTouched();
      this.chatIdControl.markAsTouched();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  onTest(baseData: BaseProviderFormData): void {
    if (this.isFormValid()) {
      const telegramData: TelegramFormData = {
        ...baseData,
        botToken: this.botTokenControl.value || '',
        chatId: this.chatIdControl.value || '',
        topicId: this.topicIdControl.value || '',
        sendSilently: this.sendSilentlyControl.value || false,
      };
      this.test.emit(telegramData);
    } else {
      this.botTokenControl.markAsTouched();
      this.chatIdControl.markAsTouched();
    }
  }
}
