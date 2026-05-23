import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent,
  EmptyStateComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { OrphanedFilesCleanerApi } from '@core/api/orphaned-files-cleaner.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import {
  OrphanedFilesCleanerConfig, OrphanedFilesClientConfig,
  ClientOrphanedFilesConfig, createDefaultClientConfig,
} from '@shared/models/orphaned-files-cleaner-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { DeferredLoader } from '@shared/utils/loading.util';

@Component({
  selector: 'app-orphaned-files-cleaner',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent,
    EmptyStateComponent, LoadingStateComponent,
  ],
  templateUrl: './orphaned-files-cleaner.component.html',
  styleUrl: './orphaned-files-cleaner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrphanedFilesCleanerComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(OrphanedFilesCleanerApi);
  private readonly toast = inject(ToastService);

  private readonly savedSnapshot = signal('');
  private readonly clientSnapshots = signal<Record<string, string>>({});

  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly clientSaving = signal(false);
  readonly clientSaved = signal(false);

  // Global settings
  readonly excludePatterns = signal<string[]>([]);
  readonly minFileAgeMinutes = signal<number | null>(0);
  readonly emptyAfterXDays = signal<number | null>(null);

  // Per-client settings
  readonly clientConfigs = signal<ClientOrphanedFilesConfig[]>([]);
  readonly selectedClientId = signal<string | null>(null);

  readonly selectedClient = computed(() =>
    this.clientConfigs().find(c => c.downloadClientId === this.selectedClientId()) ?? null
  );

  readonly clientOptions = computed<SelectOption[]>(() =>
    this.clientConfigs()
      .map(c => ({ label: c.downloadClientName, value: c.downloadClientId }))
      .sort((a, b) => a.label.localeCompare(b.label))
  );

  readonly isSelectedClientDisabled = computed(() =>
    this.selectedClient()?.downloadClientEnabled === false
  );

  readonly clientDirty = computed(() => {
    const client = this.selectedClient();
    if (!client) {
      return false;
    }
    const saved = this.clientSnapshots()[client.downloadClientId];
    if (!saved) {
      return false;
    }
    return saved !== JSON.stringify(client.clientConfig);
  });

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.loader.start();
    this.api.getConfig().subscribe({
      next: (config) => {
        this.excludePatterns.set(config.excludePatterns ?? []);
        this.minFileAgeMinutes.set(config.minFileAgeMinutes ?? 0);
        this.emptyAfterXDays.set(config.emptyAfterXDays ?? null);

        const clients = (config.clients ?? []).map(c => ({
          ...c,
          clientConfig: c.clientConfig ?? createDefaultClientConfig(),
        }));
        this.clientConfigs.set(clients);

        if (clients.length > 0) {
          this.selectedClientId.set(clients[0].downloadClientId);
        }

        const snapshots: Record<string, string> = {};
        for (const c of clients) {
          snapshots[c.downloadClientId] = JSON.stringify(c.clientConfig ?? createDefaultClientConfig());
        }
        this.clientSnapshots.set(snapshots);

        this.loader.stop();
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to load orphaned files cleaner settings');
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
  }

  onClientChange(newClientId: unknown): void {
    this.selectedClientId.set(newClientId as string | null);
  }

  updateClientField<K extends keyof OrphanedFilesClientConfig>(field: K, value: OrphanedFilesClientConfig[K]): void {
    const id = this.selectedClientId();
    if (!id) {
      return;
    }
    this.clientConfigs.update(configs =>
      configs.map(c => c.downloadClientId === id
        ? { ...c, clientConfig: { ...(c.clientConfig ?? createDefaultClientConfig()), [field]: value } }
        : c)
    );
  }

  saveClientConfig(): void {
    const clientId = this.selectedClientId();
    const client = this.selectedClient();
    if (!clientId || !client?.clientConfig) {
      return;
    }

    this.clientSaving.set(true);
    this.api.updateClientConfig(clientId, client.clientConfig).subscribe({
      next: () => {
        this.toast.success('Client settings saved');
        this.clientSaving.set(false);
        this.clientSaved.set(true);
        setTimeout(() => this.clientSaved.set(false), 1500);
        this.clientSnapshots.update(s => ({
          ...s,
          [clientId]: JSON.stringify(client.clientConfig),
        }));
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save client settings');
        this.clientSaving.set(false);
      },
    });
  }

  save(): void {
    const config: Partial<OrphanedFilesCleanerConfig> = {
      excludePatterns: this.excludePatterns(),
      minFileAgeMinutes: this.minFileAgeMinutes() ?? 0,
      emptyAfterXDays: this.emptyAfterXDays() ?? undefined,
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Orphaned files cleaner settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400 ? err.message : 'Failed to save orphaned files cleaner settings');
        this.saving.set(false);
      },
    });
  }

  private buildSnapshot(): string {
    return JSON.stringify({
      excludePatterns: this.excludePatterns(),
      minFileAgeMinutes: this.minFileAgeMinutes(),
      emptyAfterXDays: this.emptyAfterXDays(),
    });
  }

  readonly dirty = computed(() => {
    const saved = this.savedSnapshot();
    return saved !== '' && saved !== this.buildSnapshot();
  });

  hasPendingChanges(): boolean {
    return this.dirty() || this.clientDirty();
  }
}
