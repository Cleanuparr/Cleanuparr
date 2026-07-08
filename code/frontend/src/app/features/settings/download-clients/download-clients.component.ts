import { Component, ChangeDetectionStrategy, inject, signal, computed, effect } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent, LoadingStateComponent,
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
import { DeferredLoader } from '@shared/utils/loading.util';

const TYPE_OPTIONS: SelectOption[] = [
  { label: 'qBittorrent', value: DownloadClientTypeName.qBittorrent },
  { label: 'Deluge', value: DownloadClientTypeName.Deluge },
  { label: 'Transmission', value: DownloadClientTypeName.Transmission },
  { label: 'uTorrent', value: DownloadClientTypeName.uTorrent },
  { label: 'rTorrent', value: DownloadClientTypeName.rTorrent },
];

const AUTOFILL_URL_BASES: Partial<Record<DownloadClientTypeName, string>> = {
  [DownloadClientTypeName.Transmission]: 'transmission',
  [DownloadClientTypeName.rTorrent]: 'plugins/httprpc/action.php',
};

interface DownloadClientFormModel {
  enabled: boolean;
  name: string;
  typeName: DownloadClientTypeName;
  host: string;
  username: string;
  password: string;
  urlBase: string;
  externalUrl: string;
  downloadDirectorySource: string;
  downloadDirectoryTarget: string;
}

@Component({
  selector: 'app-download-clients',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, SelectComponent, ModalComponent, EmptyStateComponent,
    BadgeComponent, LoadingStateComponent, FormField,
  ],
  templateUrl: './download-clients.component.html',
  styleUrl: './download-clients.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DownloadClientsComponent implements HasPendingChanges {
  private readonly api = inject(DownloadClientApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  private readonly clientsResource = rxResource({
    stream: () => this.api.getConfig(),
  });

  readonly typeOptions = TYPE_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.clientsResource.error());
  readonly saving = signal(false);
  readonly clients = computed(() =>
    this.clientsResource.hasValue() ? (this.clientsResource.value().clients ?? []) : [],
  );

  // Modal
  readonly modalVisible = signal(false);
  readonly editingClient = signal<ClientConfig | null>(null);
  readonly testing = signal(false);

  readonly clientModel = signal<DownloadClientFormModel>({
    enabled: true, name: '', typeName: DownloadClientTypeName.qBittorrent,
    host: '', username: '', password: '', urlBase: '', externalUrl: '',
    downloadDirectorySource: '', downloadDirectoryTarget: '',
  });
  readonly clientForm = form(this.clientModel, (p) => {
    required(p.name, { message: 'Name is required' });
    required(p.host, { message: 'Host is required' });
  });

  readonly hasModalErrors = computed(() => this.clientForm().invalid());

  readonly showUsernameField = computed(() => {
    return this.clientModel().typeName !== DownloadClientTypeName.Deluge;
  });

  readonly showPasswordField = computed(() => true);

  readonly usernameHint = computed(() => {
    if (this.clientModel().typeName === DownloadClientTypeName.rTorrent) {
      return 'Username for HTTP Basic Auth';
    }
    return 'Username for authentication';
  });

  readonly passwordHint = computed(() => {
    if (this.clientModel().typeName === DownloadClientTypeName.rTorrent) {
      return 'Password for HTTP Basic Auth';
    }
    return 'Password for authentication';
  });

  readonly urlBaseHint = computed(() => {
    if (this.clientModel().typeName === DownloadClientTypeName.rTorrent) {
      return 'Path to the XMLRPC endpoint. Usually RPC2 for rTorrent or plugins/httprpc/action.php for ruTorrent.';
    }
    return 'Optional URL base path, leave blank for default';
  });

  // typeName is owned by [formField]; here we only apply type-specific defaults,
  // guarded so they never clobber values already loaded when editing a client.
  onClientTypeChange(value: unknown): void {
    const newType = value as DownloadClientTypeName;
    const m = this.clientModel();
    const patch: Partial<DownloadClientFormModel> = {};
    if (newType === DownloadClientTypeName.Deluge && m.username !== '') {
      patch.username = '';
    }
    const autofill = AUTOFILL_URL_BASES[newType];
    const replaceable = m.urlBase === '' || Object.values(AUTOFILL_URL_BASES).includes(m.urlBase);
    if (autofill && replaceable) {
      patch.urlBase = autofill;
    }
    if (Object.keys(patch).length > 0) {
      this.clientModel.update((mm) => ({ ...mm, ...patch }));
    }
  }

  constructor() {
    effect(() => {
      if (this.clientsResource.error()) {
        this.toast.error('Failed to load download clients');
      }
    });

    effect(() => {
      if (this.clientsResource.isLoading()) {
        this.loader.start();
      } else {
        this.loader.stop();
      }
    });
  }

  retry(): void {
    this.clientsResource.reload();
  }

  openAddModal(): void {
    this.editingClient.set(null);
    this.clientModel.set({
      enabled: true, name: '', typeName: DownloadClientTypeName.qBittorrent,
      host: '', username: '', password: '', urlBase: '', externalUrl: '',
      downloadDirectorySource: '', downloadDirectoryTarget: '',
    });
    this.modalVisible.set(true);
  }

  openEditModal(client: ClientConfig): void {
    this.editingClient.set(client);
    this.clientModel.set({
      enabled: client.enabled,
      name: client.name,
      typeName: client.typeName,
      host: client.host,
      username: client.username,
      password: client.password ?? '',
      urlBase: client.urlBase,
      externalUrl: client.externalUrl ?? '',
      downloadDirectorySource: client.downloadDirectorySource ?? '',
      downloadDirectoryTarget: client.downloadDirectoryTarget ?? '',
    });
    this.modalVisible.set(true);
  }

  testConnection(): void {
    const m = this.clientModel();
    const request: TestDownloadClientRequest = {
      typeName: m.typeName,
      type: DownloadClientType.Torrent,
      host: m.host,
      username: m.username,
      password: m.password,
      urlBase: m.urlBase,
      clientId: this.editingClient()?.id,
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
    if (this.clientForm().invalid()) {
      return;
    }
    const editing = this.editingClient();
    const m = this.clientModel();
    this.saving.set(true);

    if (editing) {
      const client: ClientConfig = {
        ...editing,
        enabled: m.enabled,
        name: m.name,
        typeName: m.typeName,
        host: m.host,
        username: m.username,
        password: m.password || undefined,
        urlBase: m.urlBase,
        externalUrl: m.externalUrl || undefined,
        downloadDirectorySource: m.downloadDirectorySource || null,
        downloadDirectoryTarget: m.downloadDirectoryTarget || null,
      };
      this.api.update(editing.id, client).subscribe({
        next: () => {
          this.toast.success('Client updated');
          this.modalVisible.set(false);
          this.saving.set(false);
          this.clientsResource.reload();
        },
        error: () => {
          this.toast.error('Failed to update client');
          this.saving.set(false);
        },
      });
    } else {
      const dto: CreateDownloadClientDto = {
        enabled: m.enabled,
        name: m.name,
        type: DownloadClientType.Torrent,
        typeName: m.typeName,
        host: m.host,
        username: m.username,
        password: m.password,
        urlBase: m.urlBase,
        externalUrl: m.externalUrl || undefined,
        downloadDirectorySource: m.downloadDirectorySource || null,
        downloadDirectoryTarget: m.downloadDirectoryTarget || null,
      };
      this.api.create(dto).subscribe({
        next: () => {
          this.toast.success('Client added');
          this.modalVisible.set(false);
          this.saving.set(false);
          this.clientsResource.reload();
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
    if (!confirmed) {
      return;
    }

    this.api.delete(client.id).subscribe({
      next: () => {
        this.toast.success('Client deleted');
        this.clientsResource.reload();
      },
      error: () => this.toast.error('Failed to delete client'),
    });
  }

  hasPendingChanges(): boolean {
    return false;
  }
}
