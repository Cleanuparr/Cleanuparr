import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent, ButtonComponent, InputComponent, ToggleComponent, EmptyStateComponent, LoadingStateComponent } from '@ui';
import { BlacklistSyncApi } from '@core/api/blacklist-sync.api';
import { ToastService } from '@core/services/toast.service';
import { BlacklistSyncConfig } from '@shared/models/blacklist-sync-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';

@Component({
  selector: 'app-blacklist-sync',
  standalone: true,
  imports: [PageHeaderComponent, CardComponent, ButtonComponent, InputComponent, ToggleComponent, EmptyStateComponent, LoadingStateComponent],
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

  readonly enabled = signal(false);
  readonly blacklistPath = signal('');
  private configId = '';

  readonly blacklistPathError = computed(() => {
    if (this.enabled() && !this.blacklistPath().trim()) {
      return 'This field is required when blacklist sync is enabled';
    }
    return undefined;
  });

  readonly hasErrors = computed(() => !!this.blacklistPathError());

  constructor() {
    effect(() => {
      const config = this.configResource.hasValue() ? this.configResource.value() : undefined;
      if (!config) {
        return;
      }
      untracked(() => {
        this.configId = config.id;
        this.enabled.set(config.enabled);
        this.blacklistPath.set(config.blacklistPath ?? '');
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
    const config: BlacklistSyncConfig = {
      id: this.configId,
      enabled: this.enabled(),
      blacklistPath: this.blacklistPath() || undefined,
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
    return JSON.stringify({
      enabled: this.enabled(),
      blacklistPath: this.blacklistPath(),
    });
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty();
  }
}
