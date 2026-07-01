import { Component, ChangeDetectionStrategy, inject, signal, input, computed, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { ArrApi } from '@core/api/arr.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { ArrInstance, CreateArrInstanceDto, TestArrInstanceRequest } from '@shared/models/arr-config.model';
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

interface ArrInstanceFormModel {
  name: string;
  url: string;
  externalUrl: string;
  apiKey: string;
  version: number;
  enabled: boolean;
}

@Component({
  selector: 'app-arr-settings',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, SelectComponent, ModalComponent, EmptyStateComponent,
    BadgeComponent, LoadingStateComponent, FormField,
  ],
  templateUrl: './arr-settings.component.html',
  styleUrl: './arr-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ArrSettingsComponent implements HasPendingChanges {
  private readonly api = inject(ArrApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly type = input.required<string>();
  readonly displayName = computed(() => {
    const t = this.type();
    return t.charAt(0).toUpperCase() + t.slice(1);
  });
  readonly versionOptions = computed(() => ARR_VERSION_OPTIONS[this.type()] ?? []);

  private readonly configResource = rxResource({
    params: () => this.type(),
    stream: ({ params }) => this.api.getConfig(params as ArrType),
  });

  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.configResource.error());
  readonly saving = signal(false);
  readonly instances = computed(() =>
    this.configResource.hasValue() ? (this.configResource.value().instances ?? []) : [],
  );

  // Modal state
  readonly modalVisible = signal(false);
  readonly editingInstance = signal<ArrInstance | null>(null);
  readonly testing = signal(false);

  readonly instanceModel = signal<ArrInstanceFormModel>({
    name: '', url: '', externalUrl: '', apiKey: '', version: 3, enabled: true,
  });
  readonly instanceForm = form(this.instanceModel, (p) => {
    required(p.name, { message: 'Name is required' });
    required(p.url, { message: 'URL is required' });
    required(p.apiKey, { message: 'API key is required' });
  });

  readonly hasModalErrors = computed(() => this.instanceForm().invalid());

  constructor() {
    effect(() => {
      const options = this.versionOptions();
      if (options.length > 0) {
        untracked(() => this.instanceModel.update(m => ({ ...m, version: options[0].value as number })));
      }
    });

    effect(() => {
      if (this.configResource.error()) {
        this.toast.error(`Failed to load ${this.displayName()} settings`);
      }
    });

    effect(() => {
      if (this.configResource.isLoading()) {
        this.loader.start();
      } else {
        this.loader.stop();
      }
    });
  }

  retry(): void {
    this.configResource.reload();
  }

  openAddModal(): void {
    this.editingInstance.set(null);
    const options = this.versionOptions();
    this.instanceModel.set({
      name: '', url: '', externalUrl: '', apiKey: '',
      version: options.length > 0 ? (options[0].value as number) : 3,
      enabled: true,
    });
    this.modalVisible.set(true);
  }

  openEditModal(instance: ArrInstance): void {
    this.editingInstance.set(instance);
    this.instanceModel.set({
      name: instance.name,
      url: instance.url,
      externalUrl: instance.externalUrl ?? '',
      apiKey: instance.apiKey,
      version: instance.version,
      enabled: instance.enabled,
    });
    this.modalVisible.set(true);
  }

  testConnection(): void {
    const m = this.instanceModel();
    const request: TestArrInstanceRequest = {
      url: m.url,
      apiKey: m.apiKey,
      version: m.version ?? 3,
      instanceId: this.editingInstance()?.id,
    };
    this.testing.set(true);
    this.api.testInstance(this.type() as ArrType, request).subscribe({
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
    if (this.instanceForm().invalid()) {
      return;
    }
    const m = this.instanceModel();
    const dto: CreateArrInstanceDto = {
      name: m.name,
      url: m.url,
      externalUrl: m.externalUrl || undefined,
      apiKey: m.apiKey,
      version: m.version ?? 3,
      enabled: m.enabled,
    };

    this.saving.set(true);
    const editing = this.editingInstance();
    const obs = editing?.id
      ? this.api.updateInstance(this.type() as ArrType, editing.id, dto)
      : this.api.createInstance(this.type() as ArrType, dto);

    obs.subscribe({
      next: () => {
        this.toast.success(editing ? 'Instance updated' : 'Instance added');
        this.modalVisible.set(false);
        this.saving.set(false);
        this.configResource.reload();
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

    this.api.deleteInstance(this.type() as ArrType, instance.id).subscribe({
      next: () => {
        this.toast.success('Instance deleted');
        this.configResource.reload();
      },
      error: () => this.toast.error('Failed to delete instance'),
    });
  }

  hasPendingChanges(): boolean {
    return false;
  }
}
