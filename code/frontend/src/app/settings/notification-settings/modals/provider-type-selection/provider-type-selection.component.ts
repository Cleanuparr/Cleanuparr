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
  hoveredProvider: NotificationProviderType | null = null;

  availableProviders: ProviderTypeInfo[] = [
    {
      type: NotificationProviderType.Apprise,
      name: 'Apprise',
      iconUrl: 'icons/ext/apprise-light.svg',
      iconUrlHover: 'icons/ext/apprise.svg',
      description: 'https://github.com/caronc/apprise'
    },
    {
      type: NotificationProviderType.Notifiarr,
      name: 'Notifiarr',
      iconUrl: 'icons/ext/notifiarr-light.svg',
      iconUrlHover: 'icons/ext/notifiarr.svg',
      description: 'https://notifiarr.com'
    },
    {
      type: NotificationProviderType.Ntfy,
      name: 'ntfy',
      iconUrl: 'icons/ext/ntfy-light.svg',
      iconUrlHover: 'icons/ext/ntfy.svg',
      description: 'https://ntfy.sh/'
    }
  ];

  selectProvider(type: NotificationProviderType) {
    this.providerSelected.emit(type);
  }

  onProviderEnter(type: NotificationProviderType) {
    this.hoveredProvider = type;
  }

  onProviderLeave() {
    this.hoveredProvider = null;
  }

  onCancel() {
    this.cancel.emit();
  }
}
