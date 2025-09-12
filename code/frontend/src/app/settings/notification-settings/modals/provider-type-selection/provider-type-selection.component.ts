import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { NotificationProviderType } from '../../../../shared/models/enums';
import { ProviderTypeInfo } from '../../models/provider-modal.model';

@Component({
  selector: 'app-provider-type-selection',
  standalone: true,
  imports: [
    CommonModule,
    DialogModule,
    ButtonModule
  ],
  templateUrl: './provider-type-selection.component.html',
  styleUrls: ['./provider-type-selection.component.scss']
})
export class ProviderTypeSelectionComponent {
  @Input() visible = false;
  @Output() providerSelected = new EventEmitter<NotificationProviderType>();
  @Output() cancel = new EventEmitter<void>();

  // Available providers - only show implemented ones
  availableProviders: ProviderTypeInfo[] = [
    {
      type: NotificationProviderType.Notifiarr,
      name: 'Notifiarr',
      iconUrl: 'https://cdn.jsdelivr.net/gh/selfhst/icons/svg/notifiarr.svg',
      description: 'https://notifiarr.com'
    },
    {
      type: NotificationProviderType.Apprise,
      name: 'Apprise',
      iconUrl: 'https://cdn.jsdelivr.net/gh/selfhst/icons/svg/apprise.svg',
      description: 'https://github.com/caronc/apprise'
    },
    {
      type: NotificationProviderType.Ntfy,
      name: 'ntfy',
      iconUrl: 'https://cdn.jsdelivr.net/gh/selfhst/icons/svg/ntfy.svg',
      description: 'https://ntfy.sh/'
    }
  ];

  selectProvider(type: NotificationProviderType) {
    this.providerSelected.emit(type);
  }

  onCancel() {
    this.cancel.emit();
  }
}
