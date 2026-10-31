import {
  Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, viewChildren,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { form, required, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent, LoadingStateComponent, NewBadgeComponent,
} from '@ui';
import { ArrApi } from '@core/api/arr.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { ThemeService } from '@core/services/theme.service';
import { ConfirmService } from '@core/services/confirm.service';
import { ArrInstance, CreateArrInstanceDto, TestArrInstanceRequest } from '@shared/models/arr-config.model';
import type { ArrType } from '@shared/models/enums';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { createSettingsResource } from '@shared/utils/settings-resource.util';
import { ARR_TYPES, ARR_APP_META } from './arr-app-meta';
import { ArrSectionComponent } from './arr-section.component';

interface ArrInstanceFormModel {
  name: string;
  url: string;
  externalUrl: string;
  apiKey: string;
  version: number;
  enabled: boolean;
}

@Component({
  selector: 'app-arr-apps',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent, ToggleComponent,
    SelectComponent, ModalComponent, EmptyStateComponent, BadgeComponent, LoadingStateComponent,
    NewBadgeComponent, FormField, ArrSectionComponent,
  ],
  templateUrl: './arr-apps.component.html',
  styleUrl: './arr-apps.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ArrAppsComponent implements HasPendingChanges {
  private readonly api = inject(ArrApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  private readonly route = inject(ActivatedRoute);
  protected readonly themeService = inject(ThemeService);

  protected readonly ARR_APP_META = ARR_APP_META;

  private readonly settings = createSettingsResource({
    params: () => true,
    load: () => forkJoin(ARR_TYPES.map(type =>
      this.api.getConfig(type),
    )),
    errorMessage: 'Failed to load media manager settings',
  });
  readonly configResource = this.settings.resource;

  readonly loader = this.settings.loader;
  readonly loadError = this.settings.loadError;

  private readonly allConfigs = computed(() => {
    if (!this.configResource.hasValue()) {
      return [];
    }
    const configs = this.configResource.value();
    return ARR_TYPES.map((type, index) => ({
      type,
      config: configs[index],
    }));
  });

  readonly configured = computed(() =>
    this.allConfigs().filter(e => (e.config.instances ?? []).length > 0),
  );

  readonly unconfigured = computed(() =>
    this.allConfigs().filter(e => (e.config.instances ?? []).length === 0).map(e => e.type),
  );

  readonly sections = viewChildren(ArrSectionComponent);

  // Modal state
  readonly modalType = signal<ArrType>('sonarr');
  readonly modalVisible = signal(false);
  readonly editingInstance = signal<ArrInstance | null>(null);
  readonly testing = signal(false);
  readonly saving = signal(false);

  readonly modalMeta = computed(() => ARR_APP_META[this.modalType()]);
  readonly modalDisplayName = computed(() => this.modalMeta().displayName);
  readonly modalIsEdit = computed(() => !!this.editingInstance());

  readonly instanceModel = signal<ArrInstanceFormModel>({
    name: '', url: '', externalUrl: '', apiKey: '', version: 3, enabled: true,
  });
  readonly instanceForm = form(this.instanceModel, (p) => {
    required(p.name, { message: 'Name is required' });
    required(p.url, { message: 'URL is required' });
    required(p.apiKey, { message: 'API key is required' });
  });

  readonly hasModalErrors = computed(() => this.instanceForm().invalid());

  private readonly openSnapshot = signal('');
  /** Reloads after a save must not yank the page back to the deep-linked section. */
  private fragmentScrolled = false;
  private readonly modalDirty = computed(() =>
    this.modalVisible() && JSON.stringify(this.instanceModel()) !== this.openSnapshot());

  constructor() {
    effect(() => {
      if (!this.fragmentScrolled && !this.configResource.isLoading() && this.configResource.hasValue()) {
        this.fragmentScrolled = true;
        const fragment = untracked(() => this.route.snapshot.fragment);
        if (fragment) {
          queueMicrotask(() => {
            document.getElementById(fragment)?.scrollIntoView({ block: 'start' });
          });
        }
      }
    });
  }

  retry(): void {
    this.settings.retry();
  }

  openAddModal(type: ArrType): void {
    this.modalType.set(type);
    this.editingInstance.set(null);
    const options = ARR_APP_META[type].versionOptions;
    this.instanceModel.set({
      name: '', url: '', externalUrl: '', apiKey: '',
      version: options.length > 0 ? (options[0].value as number) : 3,
      enabled: true,
    });
    this.openSnapshot.set(JSON.stringify(this.instanceModel()));
    this.modalVisible.set(true);
  }

  openEditModal(type: ArrType, instance: ArrInstance): void {
    this.modalType.set(type);
    this.editingInstance.set(instance);
    this.instanceModel.set({
      name: instance.name,
      url: instance.url,
      externalUrl: instance.externalUrl ?? '',
      apiKey: instance.apiKey,
      version: instance.version,
      enabled: instance.enabled,
    });
    this.openSnapshot.set(JSON.stringify(this.instanceModel()));
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
    this.api.testInstance(this.modalType(), request).subscribe({
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
      ? this.api.updateInstance(this.modalType(), editing.id, dto)
      : this.api.createInstance(this.modalType(), dto);

    obs.subscribe({
      next: () => {
        this.toast.success(editing ? 'Instance updated' : 'Instance added');
        this.modalVisible.set(false);
        this.saving.set(false);
        this.configResource.reload();
      },
      error: (err: ApiError) => {
        this.toast.error(err.message);
        this.saving.set(false);
      },
    });
  }

  async deleteInstance(type: ArrType, instance: ArrInstance): Promise<void> {
    if (!instance.id) return;
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Instance',
      message: `Are you sure you want to delete "${instance.name}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed) return;

    this.api.deleteInstance(type, instance.id).subscribe({
      next: () => {
        this.toast.success('Instance deleted');
        this.configResource.reload();
      },
      error: (err: ApiError) => this.toast.error(err.message),
    });
  }

  hasPendingChanges(): boolean {
    return this.modalDirty() || this.sections().some(s => s.hasPendingChanges());
  }
}
