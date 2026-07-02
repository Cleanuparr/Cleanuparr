import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, ModalComponent, EmptyStateComponent,
  BadgeComponent, LoadingStateComponent,
} from '@ui';
import { NotificationApi } from '@core/api/notification.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { ThemeService } from '@core/services/theme.service';
import { NotificationProviderDto } from '@shared/models/notification-provider.model';
import { NotificationProviderType } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';
import { NotificationProviderModalComponent } from './notification-provider-modal.component';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, ModalComponent,
    EmptyStateComponent, BadgeComponent, LoadingStateComponent,
    NotificationProviderModalComponent,
  ],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsComponent implements HasPendingChanges {
  private readonly api = inject(NotificationApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly themeService = inject(ThemeService);

  private readonly providersResource = rxResource({
    stream: () => this.api.getProviders(),
  });

  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.providersResource.error());
  readonly providers = computed(() =>
    this.providersResource.hasValue() ? (this.providersResource.value().providers ?? []) : [],
  );

  // Selection modal
  readonly selectionModalVisible = signal(false);

  // Config modal
  readonly modalVisible = signal(false);
  readonly editingProvider = signal<NotificationProviderDto | null>(null);
  readonly selectedType = signal<NotificationProviderType>(NotificationProviderType.Discord);

  // Provider selection data
  readonly availableProviders = [
    { type: NotificationProviderType.Apprise, name: 'Apprise', iconUrl: 'icons/ext/apprise.svg', iconLightUrl: 'icons/ext/apprise-light.svg', description: 'github.com/caronc/apprise' },
    { type: NotificationProviderType.Discord, name: 'Discord', iconUrl: 'icons/ext/discord.svg', iconLightUrl: 'icons/ext/discord-light.svg', description: 'discord.com' },
    { type: NotificationProviderType.Gotify, name: 'Gotify', iconUrl: 'icons/ext/gotify.svg', iconLightUrl: 'icons/ext/gotify-light.svg', description: 'gotify.net' },
    { type: NotificationProviderType.Notifiarr, name: 'Notifiarr', iconUrl: 'icons/ext/notifiarr.svg', iconLightUrl: 'icons/ext/notifiarr-light.svg', description: 'notifiarr.com' },
    { type: NotificationProviderType.Ntfy, name: 'ntfy', iconUrl: 'icons/ext/ntfy.svg', iconLightUrl: 'icons/ext/ntfy-light.svg', description: 'ntfy.sh' },
    { type: NotificationProviderType.Pushover, name: 'Pushover', iconUrl: 'icons/ext/pushover.svg', iconLightUrl: 'icons/ext/pushover-light.svg', description: 'pushover.net' },
    { type: NotificationProviderType.Telegram, name: 'Telegram', iconUrl: 'icons/ext/telegram.svg', iconLightUrl: 'icons/ext/telegram-light.svg', description: 'core.telegram.org/bots' },
  ];

  constructor() {
    effect(() => {
      if (this.providersResource.error()) {
        this.toast.error('Failed to load notification providers');
      }
    });

    effect(() => {
      if (this.providersResource.isLoading()) {
        this.loader.start();
      } else {
        this.loader.stop();
      }
    });
  }

  retry(): void {
    this.providersResource.reload();
  }

  openAddModal(): void {
    this.selectionModalVisible.set(true);
  }

  onProviderTypeSelected(type: NotificationProviderType): void {
    this.selectionModalVisible.set(false);
    this.editingProvider.set(null);
    this.selectedType.set(type);
    this.modalVisible.set(true);
  }

  openEditModal(provider: NotificationProviderDto): void {
    this.editingProvider.set(provider);
    this.modalVisible.set(true);
  }

  onProviderSaved(): void {
    this.providersResource.reload();
  }

  async deleteProvider(provider: NotificationProviderDto): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Provider',
      message: `Are you sure you want to delete "${provider.name}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed) return;

    this.api.deleteProvider(provider.id).subscribe({
      next: () => {
        this.toast.success('Provider deleted');
        this.providersResource.reload();
      },
      error: () => this.toast.error('Failed to delete provider'),
    });
  }

  hasPendingChanges(): boolean {
    return false;
  }
}
