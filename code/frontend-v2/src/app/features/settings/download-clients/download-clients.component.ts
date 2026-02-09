import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent,
  type SelectOption,
} from '@ui';
import { DownloadClientApi } from '@core/api/download-client.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import {
  ClientConfig, CreateDownloadClientDto, TestDownloadClientRequest,
} from '@shared/models/download-client-config.model';
import { DownloadClientType, DownloadClientTypeName } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';

const TYPE_OPTIONS: SelectOption[] = [
  { label: 'qBittorrent', value: DownloadClientTypeName.qBittorrent },
  { label: 'Deluge', value: DownloadClientTypeName.Deluge },
  { label: 'Transmission', value: DownloadClientTypeName.Transmission },
  { label: 'uTorrent', value: DownloadClientTypeName.uTorrent },
];

@Component({
  selector: 'app-download-clients',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, SelectComponent, ModalComponent, EmptyStateComponent,
    BadgeComponent,
  ],
  templateUrl: './download-clients.component.html',
  styleUrl: './download-clients.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DownloadClientsComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(DownloadClientApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly typeOptions = TYPE_OPTIONS;
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly clients = signal<ClientConfig[]>([]);

  // Modal
  readonly modalVisible = signal(false);
  readonly editingClient = signal<ClientConfig | null>(null);
  readonly modalEnabled = signal(true);
  readonly modalName = signal('');
  readonly modalTypeName = signal<unknown>(DownloadClientTypeName.qBittorrent);
  readonly modalHost = signal('');
  readonly modalUsername = signal('');
  readonly modalPassword = signal('');
  readonly modalUrlBase = signal('');
  readonly testing = signal(false);

  // Modal validation
  readonly modalNameError = computed(() => {
    if (!this.modalName().trim()) return 'Name is required';
    return undefined;
  });
  readonly modalHostError = computed(() => {
    if (!this.modalHost().trim()) return 'Host is required';
    return undefined;
  });
  readonly hasModalErrors = computed(() => !!(
    this.modalNameError() || this.modalHostError()
  ));

  readonly showUsernameField = computed(() => {
    return this.modalTypeName() !== DownloadClientTypeName.Deluge;
  });

  onClientTypeChange(value: unknown): void {
    this.modalTypeName.set(value);
    if (value === DownloadClientTypeName.Deluge) {
      this.modalUsername.set('');
    }
    if (value === DownloadClientTypeName.Transmission) {
      this.modalUrlBase.set('transmission');
    }
  }

  ngOnInit(): void {
    this.loadClients();
  }

  private loadClients(): void {
    this.loading.set(true);
    this.api.getConfig().subscribe({
      next: (config) => {
        this.clients.set(config.clients ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.toast.error('Failed to load download clients');
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadClients();
  }

  openAddModal(): void {
    this.editingClient.set(null);
    this.modalEnabled.set(true);
    this.modalName.set('');
    this.modalTypeName.set(DownloadClientTypeName.qBittorrent);
    this.modalHost.set('');
    this.modalUsername.set('');
    this.modalPassword.set('');
    this.modalUrlBase.set('');
    this.modalVisible.set(true);
  }

  openEditModal(client: ClientConfig): void {
    this.editingClient.set(client);
    this.modalEnabled.set(client.enabled);
    this.modalName.set(client.name);
    this.modalTypeName.set(client.typeName);
    this.modalHost.set(client.host);
    this.modalUsername.set(client.username);
    this.modalPassword.set('');
    this.modalUrlBase.set(client.urlBase);
    this.modalVisible.set(true);
  }

  testConnection(): void {
    const request: TestDownloadClientRequest = {
      typeName: this.modalTypeName() as DownloadClientTypeName,
      type: DownloadClientType.Torrent,
      host: this.modalHost(),
      username: this.modalUsername(),
      password: this.modalPassword(),
      urlBase: this.modalUrlBase(),
    };
    this.testing.set(true);
    this.api.test(request).subscribe({
      next: (result) => {
        this.toast.success(result.message || 'Connection successful');
        this.testing.set(false);
      },
      error: () => {
        this.toast.error('Connection test failed');
        this.testing.set(false);
      },
    });
  }

  saveClient(): void {
    if (this.hasModalErrors()) return;
    const editing = this.editingClient();
    this.saving.set(true);

    if (editing) {
      const client: ClientConfig = {
        ...editing,
        enabled: this.modalEnabled(),
        name: this.modalName(),
        typeName: this.modalTypeName() as DownloadClientTypeName,
        host: this.modalHost(),
        username: this.modalUsername(),
        password: this.modalPassword() || undefined,
        urlBase: this.modalUrlBase(),
      };
      this.api.update(editing.id, client).subscribe({
        next: () => {
          this.toast.success('Client updated');
          this.modalVisible.set(false);
          this.saving.set(false);
          this.loadClients();
        },
        error: () => {
          this.toast.error('Failed to update client');
          this.saving.set(false);
        },
      });
    } else {
      const dto: CreateDownloadClientDto = {
        enabled: this.modalEnabled(),
        name: this.modalName(),
        type: DownloadClientType.Torrent,
        typeName: this.modalTypeName() as DownloadClientTypeName,
        host: this.modalHost(),
        username: this.modalUsername(),
        password: this.modalPassword(),
        urlBase: this.modalUrlBase(),
      };
      this.api.create(dto).subscribe({
        next: () => {
          this.toast.success('Client added');
          this.modalVisible.set(false);
          this.saving.set(false);
          this.loadClients();
        },
        error: () => {
          this.toast.error('Failed to add client');
          this.saving.set(false);
        },
      });
    }
  }

  async deleteClient(client: ClientConfig): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Client',
      message: `Are you sure you want to delete "${client.name}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed) return;

    this.api.delete(client.id).subscribe({
      next: () => {
        this.toast.success('Client deleted');
        this.loadClients();
      },
      error: () => this.toast.error('Failed to delete client'),
    });
  }

  hasPendingChanges(): boolean {
    return false;
  }
}
