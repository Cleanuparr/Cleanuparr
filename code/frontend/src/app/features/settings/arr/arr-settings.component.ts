import { Component, ChangeDetectionStrategy, inject, signal, input, computed, effect, untracked } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { ArrApi } from '@core/api/arr.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { ArrConfig, ArrInstance, CreateArrInstanceDto, TestArrInstanceRequest } from '@shared/models/arr-config.model';
import { ArrType } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';

const ARR_VERSION_OPTIONS: Record<string, SelectOption[]> = {
  sonarr:  [{ label: 'v4', value: 4 }],
  radarr:  [{ label: 'v6', value: 6 }],
  lidarr:  [{ label: 'v3', value: 3 }],
  readarr: [{ label: 'v0.4', value: 0.4 }],
  whisparr: [{ label: 'v2', value: 2 }, { label: 'v3', value: 3 }],
};

@Component({
  selector: 'app-arr-settings',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, SelectComponent, ModalComponent, EmptyStateComponent,
    BadgeComponent, LoadingStateComponent,
  ],
  templateUrl: './arr-settings.component.html',
  styleUrl: './arr-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ArrSettingsComponent implements HasPendingChanges {
  private readonly api = inject(ArrApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly arrType = input.required<string>({ alias: 'type' });
  readonly displayName = computed(() => {
    const t = this.arrType();
    return t.charAt(0).toUpperCase() + t.slice(1);
  });
  readonly versionOptions = computed(() => ARR_VERSION_OPTIONS[this.arrType()] ?? []);

  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly instances = signal<ArrInstance[]>([]);

  // Modal state
  readonly modalVisible = signal(false);
  readonly editingInstance = signal<ArrInstance | null>(null);
  readonly modalName = signal('');
  readonly modalUrl = signal('');
  readonly modalApiKey = signal('');
  readonly modalVersion = signal<unknown>(3);
  readonly modalEnabled = signal(true);
  readonly testing = signal(false);

  // Modal validation
  readonly modalNameError = computed(() => {
    if (!this.modalName().trim()) return 'Name is required';
    return undefined;
  });
  readonly modalUrlError = computed(() => {
    if (!this.modalUrl().trim()) return 'URL is required';
    return undefined;
  });
  readonly modalApiKeyError = computed(() => {
    if (!this.modalApiKey().trim()) return 'API key is required';
    return undefined;
  });
  readonly hasModalErrors = computed(() => !!(
    this.modalNameError() || this.modalUrlError() || this.modalApiKeyError()
  ));

  constructor() {
    effect(() => {
      const type = this.arrType();
      if (type) {
        untracked(() => {
          this.instances.set([]);
          this.loadError.set(false);
          this.loadConfig();
        });
      }
    });

    effect(() => {
      const options = this.versionOptions();
      if (options.length > 0) {
        untracked(() => this.modalVersion.set(options[0].value));
      }
    });
  }

  private loadConfig(): void {
    this.loader.start();
    this.api.getConfig(this.arrType() as ArrType).subscribe({
      next: (config) => {
        this.instances.set(config.instances ?? []);
        this.loader.stop();
      },
      error: () => {
        this.toast.error(`Failed to load ${this.displayName()} settings`);
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
  }

  openAddModal(): void {
    this.editingInstance.set(null);
    this.modalName.set('');
    this.modalUrl.set('');
    this.modalApiKey.set('');
    const options = this.versionOptions();
    this.modalVersion.set(options.length > 0 ? options[0].value : 3);
    this.modalEnabled.set(true);
    this.modalVisible.set(true);
  }

  openEditModal(instance: ArrInstance): void {
    this.editingInstance.set(instance);
    this.modalName.set(instance.name);
    this.modalUrl.set(instance.url);
    this.modalApiKey.set(instance.apiKey);
    this.modalVersion.set(instance.version);
    this.modalEnabled.set(instance.enabled);
    this.modalVisible.set(true);
  }

  testConnection(): void {
    const request: TestArrInstanceRequest = {
      url: this.modalUrl(),
      apiKey: this.modalApiKey(),
      version: (this.modalVersion() as number) ?? 3,
    };
    this.testing.set(true);
    this.api.testInstance(this.arrType() as ArrType, request).subscribe({
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

  saveInstance(): void {
    if (this.hasModalErrors()) return;
    const dto: CreateArrInstanceDto = {
      name: this.modalName(),
      url: this.modalUrl(),
      apiKey: this.modalApiKey(),
      version: (this.modalVersion() as number) ?? 3,
      enabled: this.modalEnabled(),
    };

    this.saving.set(true);
    const editing = this.editingInstance();
    const obs = editing?.id
      ? this.api.updateInstance(this.arrType() as ArrType, editing.id, dto)
      : this.api.createInstance(this.arrType() as ArrType, dto);

    obs.subscribe({
      next: () => {
        this.toast.success(editing ? 'Instance updated' : 'Instance added');
        this.modalVisible.set(false);
        this.saving.set(false);
        this.loadConfig();
      },
      error: () => {
        this.toast.error('Failed to save instance');
        this.saving.set(false);
      },
    });
  }

  async deleteInstance(instance: ArrInstance): Promise<void> {
    if (!instance.id) return;
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Instance',
      message: `Are you sure you want to delete "${instance.name}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed) return;

    this.api.deleteInstance(this.arrType() as ArrType, instance.id).subscribe({
      next: () => {
        this.toast.success('Instance deleted');
        this.loadConfig();
      },
      error: () => this.toast.error('Failed to delete instance'),
    });
  }

  hasPendingChanges(): boolean {
    return false;
  }
}
