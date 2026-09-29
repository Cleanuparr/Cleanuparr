import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked } from '@angular/core';
import { form, required, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent, ButtonComponent, InputComponent, ToggleComponent, EmptyStateComponent, LoadingStateComponent } from '@ui';
import { BlacklistSyncApi } from '@core/api/blacklist-sync.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { BlacklistSyncConfig } from '@shared/models/blacklist-sync-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { createSettingsResource } from '@shared/utils/settings-resource.util';
import { createDirtyTracker, SAVED_FLASH_MS } from '@shared/utils/dirty-tracker.util';

interface BlacklistSyncFormModel {
  enabled: boolean;
  blacklistPath: string;
}

@Component({
  selector: 'app-blacklist-sync',
  standalone: true,
  imports: [PageHeaderComponent, CardComponent, ButtonComponent, InputComponent, ToggleComponent, EmptyStateComponent, LoadingStateComponent, FormField],
  templateUrl: './blacklist-sync.component.html',
  styleUrl: './blacklist-sync.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BlacklistSyncComponent implements HasPendingChanges {
  private readonly api = inject(BlacklistSyncApi);
  private readonly toast = inject(ToastService);

  private readonly model = signal<BlacklistSyncFormModel>({ enabled: false, blacklistPath: '' });

  private readonly settings = createSettingsResource({
    load: () => this.api.getConfig(),
  });
  private readonly configResource = this.settings.resource;

  readonly loader = this.settings.loader;
  readonly loadError = this.settings.loadError;
  readonly saving = signal(false);
  readonly saved = signal(false);

  private readonly dirtyTracker = createDirtyTracker(this.model);
  readonly dirty = this.dirtyTracker.dirty;

  readonly bsForm = form(this.model, (p) => {
    required(p.blacklistPath, {
      when: () => this.model().enabled,
      message: 'This field is required when blacklist sync is enabled',
    });
  });

  readonly hasErrors = computed(() => this.bsForm().invalid());

  constructor() {
    effect(() => {
      const config = this.configResource.hasValue() ? this.configResource.value() : undefined;
      if (!config) {
        return;
      }
      untracked(() => {
        this.model.set({ enabled: config.enabled, blacklistPath: config.blacklistPath ?? '' });
        this.dirtyTracker.markSaved();
      });
    });
  }

  retry(): void {
    this.settings.retry();
  }

  save(): void {
    const m = this.model();
    const config: BlacklistSyncConfig = {
      enabled: m.enabled,
      blacklistPath: m.blacklistPath || undefined,
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Blacklist sync settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), SAVED_FLASH_MS);
        this.dirtyTracker.markSaved(m);
      },
      error: (err: ApiError) => {
        this.toast.error(err.message);
        this.saving.set(false);
      },
    });
  }

  hasPendingChanges(): boolean {
    return this.dirty();
  }
}
