import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent, ButtonComponent, InputComponent, ToggleComponent } from '@ui';
import { BlacklistSyncApi } from '@core/api/blacklist-sync.api';
import { ToastService } from '@core/services/toast.service';
import { BlacklistSyncConfig } from '@shared/models/blacklist-sync-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';

@Component({
  selector: 'app-blacklist-sync',
  standalone: true,
  imports: [PageHeaderComponent, CardComponent, ButtonComponent, InputComponent, ToggleComponent],
  templateUrl: './blacklist-sync.component.html',
  styleUrl: './blacklist-sync.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BlacklistSyncComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(BlacklistSyncApi);
  private readonly toast = inject(ToastService);

  private savedSnapshot = '';

  readonly loading = signal(false);
  readonly saving = signal(false);

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

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.loading.set(true);
    this.api.getConfig().subscribe({
      next: (config) => {
        this.configId = config.id;
        this.enabled.set(config.enabled);
        this.blacklistPath.set(config.blacklistPath ?? '');
        this.loading.set(false);
        this.savedSnapshot = this.buildSnapshot();
      },
      error: () => {
        this.toast.error('Failed to load blacklist sync settings');
        this.loading.set(false);
      },
    });
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
        this.savedSnapshot = this.buildSnapshot();
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

  hasPendingChanges(): boolean {
    return this.savedSnapshot !== '' && this.savedSnapshot !== this.buildSnapshot();
  }
}
