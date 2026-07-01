import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { form, required, FormField } from '@angular/forms/signals';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent, ButtonComponent, InputComponent, ToggleComponent, EmptyStateComponent, LoadingStateComponent } from '@ui';
import { BlacklistSyncApi } from '@core/api/blacklist-sync.api';
import { ToastService } from '@core/services/toast.service';
import { BlacklistSyncConfig } from '@shared/models/blacklist-sync-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';

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

  private readonly savedSnapshot = signal('');

  private readonly configResource = rxResource({
    stream: () => this.api.getConfig(),
  });

  readonly loader = new DeferredLoader();
  readonly loadError = computed(() => !!this.configResource.error());
  readonly saving = signal(false);
  readonly saved = signal(false);

  private readonly model = signal<BlacklistSyncFormModel>({ enabled: false, blacklistPath: '' });
  private configId = '';

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
        this.configId = config.id;
        this.model.set({ enabled: config.enabled, blacklistPath: config.blacklistPath ?? '' });
        this.savedSnapshot.set(this.buildSnapshot());
      });
    });

    effect(() => {
      if (this.configResource.error()) {
        this.toast.error('Failed to load blacklist sync settings');
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

  save(): void {
    const m = this.model();
    const config: BlacklistSyncConfig = {
      id: this.configId,
      enabled: m.enabled,
      blacklistPath: m.blacklistPath || undefined,
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Blacklist sync settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to save blacklist sync settings');
        this.saving.set(false);
      },
    });
  }

  private buildSnapshot(): string {
    return JSON.stringify(this.model());
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty();
  }
}
