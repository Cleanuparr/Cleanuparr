import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { forkJoin } from 'rxjs';
import { form, required, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { DownloadClientApi, indexClientTypes } from '@core/api/download-client.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import {
  DownloadClientTypeInfo, ClientConfig, CreateDownloadClientDto, UpdateDownloadClientDto, TestDownloadClientRequest,
} from '@shared/models/download-client-config.model';
import { DownloadClientAuthField, DownloadClientTypeName } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { createSettingsResource } from '@shared/utils/settings-resource.util';

const TYPE_OPTIONS: SelectOption[] = [
  { label: 'qBittorrent', value: DownloadClientTypeName.qBittorrent },
  { label: 'Deluge', value: DownloadClientTypeName.Deluge },
  { label: 'Transmission', value: DownloadClientTypeName.Transmission },
  { label: 'uTorrent', value: DownloadClientTypeName.uTorrent },
  { label: 'rTorrent', value: DownloadClientTypeName.rTorrent },
  { label: 'SABnzbd', value: DownloadClientTypeName.Sabnzbd },
];

const AUTOFILL_URL_BASES: Partial<Record<DownloadClientTypeName, string>> = {
  [DownloadClientTypeName.Transmission]: 'transmission',
  [DownloadClientTypeName.rTorrent]: 'plugins/httprpc/action.php',
};

type AuthField = 'username' | 'password' | 'apiKey';

const AUTH_FIELD_BY_API_FIELD: Record<DownloadClientAuthField, AuthField> = {
  [DownloadClientAuthField.Username]: 'username',
  [DownloadClientAuthField.Password]: 'password',
  [DownloadClientAuthField.ApiKey]: 'apiKey',
};

interface DownloadClientFormModel {
  enabled: boolean;
  name: string;
  typeName: DownloadClientTypeName;
  host: string;
  username: string;
  password: string;
  apiKey: string;
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

  private readonly settings = createSettingsResource({
    load: () => forkJoin({ config: this.api.getConfig(), types: this.api.getTypes() }),
    errorMessage: 'Failed to load download clients',
  });
  private readonly loadResource = this.settings.resource;

  /** Type info indexed by type name, from the backend; empty while loading or on error. */
  private readonly typesByName = computed<Partial<Record<DownloadClientTypeName, DownloadClientTypeInfo>>>(() =>
    this.loadResource.hasValue() ? indexClientTypes(this.loadResource.value().types) : {});

  private readonly authFieldsByType = computed<Partial<Record<DownloadClientTypeName, AuthField[]>>>(() => {
    const result: Partial<Record<DownloadClientTypeName, AuthField[]>> = {};
    for (const [typeName, info] of Object.entries(this.typesByName()) as [DownloadClientTypeName, DownloadClientTypeInfo][]) {
      result[typeName] = info.authFields.map((f) => AUTH_FIELD_BY_API_FIELD[f]);
    }
    return result;
  });

  readonly typeOptions = TYPE_OPTIONS;
  readonly loader = this.settings.loader;
  readonly loadError = this.settings.loadError;
  readonly saving = signal(false);
  readonly clients = computed(() =>
    this.loadResource.hasValue() ? (this.loadResource.value().config.clients ?? []) : [],
  );

  // Modal
  readonly modalVisible = signal(false);
  readonly editingClient = signal<ClientConfig | null>(null);
  readonly testing = signal(false);

  readonly clientModel = signal<DownloadClientFormModel>({
    enabled: true, name: '', typeName: DownloadClientTypeName.qBittorrent,
    host: '', username: '', password: '', apiKey: '', urlBase: '', externalUrl: '',
    downloadDirectorySource: '', downloadDirectoryTarget: '',
  });
  readonly clientForm = form(this.clientModel, (p) => {
    required(p.name, { message: 'Name is required' });
    required(p.host, { message: 'Host is required' });
    required(p.apiKey, {
      when: ({ valueOf }) => valueOf(p.typeName) === DownloadClientTypeName.Sabnzbd,
      message: 'API key is required',
    });
  });

  readonly hasModalErrors = computed(() => this.clientForm().invalid());

  /** JSON snapshot of the model as loaded when the modal opened, for dirty tracking. */
  private readonly openSnapshot = signal('');
  private readonly modalDirty = computed(() =>
    this.modalVisible() && JSON.stringify(this.clientModel()) !== this.openSnapshot());

  readonly showUsernameField = computed(() =>
    (this.authFieldsByType()[this.clientModel().typeName] ?? []).includes('username'));

  readonly showPasswordField = computed(() =>
    (this.authFieldsByType()[this.clientModel().typeName] ?? []).includes('password'));

  readonly showApiKeyField = computed(() =>
    (this.authFieldsByType()[this.clientModel().typeName] ?? []).includes('apiKey'));

  readonly apiKeyHint = 'API key from SABnzbd > Config > General';

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
    const fields = this.authFieldsByType()[newType] ?? [];
    if (!fields.includes('username') && m.username !== '') {
      patch.username = '';
    }
    if (!fields.includes('password') && m.password !== '') {
      patch.password = '';
    }
    if (!fields.includes('apiKey') && m.apiKey !== '') {
      patch.apiKey = '';
    }
    const autofill = AUTOFILL_URL_BASES[newType];
    const replaceable = m.urlBase === '' || Object.values(AUTOFILL_URL_BASES).includes(m.urlBase);
    if (replaceable && (autofill ?? '') !== m.urlBase) {
      patch.urlBase = autofill ?? '';
    }
    if (Object.keys(patch).length > 0) {
      this.clientModel.update((mm) => ({ ...mm, ...patch }));
    }
  }

  retry(): void {
    this.settings.retry();
  }

  openAddModal(): void {
    this.editingClient.set(null);
    this.clientModel.set({
      enabled: true, name: '', typeName: DownloadClientTypeName.qBittorrent,
      host: '', username: '', password: '', apiKey: '', urlBase: '', externalUrl: '',
      downloadDirectorySource: '', downloadDirectoryTarget: '',
    });
    this.openSnapshot.set(JSON.stringify(this.clientModel()));
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
      apiKey: client.apiKey ?? '',
      urlBase: client.urlBase,
      externalUrl: client.externalUrl ?? '',
      downloadDirectorySource: client.downloadDirectorySource ?? '',
      downloadDirectoryTarget: client.downloadDirectoryTarget ?? '',
    });
    this.openSnapshot.set(JSON.stringify(this.clientModel()));
    this.modalVisible.set(true);
  }

  testConnection(): void {
    const m = this.clientModel();
    const request: TestDownloadClientRequest = {
      typeName: m.typeName,
      host: m.host,
      username: m.username,
      password: m.password,
      apiKey: m.apiKey,
      urlBase: m.urlBase,
      clientId: this.editingClient()?.id,
    };
    this.testing.set(true);
    this.api.test(request).subscribe({
      next: (result) => {
        this.toast.success(result.message || 'Connection successful');
        this.testing.set(false);
      },
      error: (err: ApiError) => {
        this.toast.error(err.message);
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
      const client: UpdateDownloadClientDto = {
        enabled: m.enabled,
        name: m.name,
        typeName: m.typeName,
        host: m.host,
        username: m.username,
        password: m.password || undefined,
        apiKey: m.apiKey || undefined,
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
          this.loadResource.reload();
        },
        error: (err: ApiError) => {
          this.toast.error(err.message);
          this.saving.set(false);
        },
      });
    } else {
      const dto: CreateDownloadClientDto = {
        enabled: m.enabled,
        name: m.name,
        typeName: m.typeName,
        host: m.host,
        username: m.username,
        password: m.password,
        apiKey: m.apiKey,
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
          this.loadResource.reload();
        },
        error: (err: ApiError) => {
          this.toast.error(err.message);
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
        this.loadResource.reload();
      },
      error: (err: ApiError) => this.toast.error(err.message),
    });
  }

  hasPendingChanges(): boolean {
    return this.modalDirty();
  }
}
