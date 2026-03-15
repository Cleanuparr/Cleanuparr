import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, effect, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import {
  CardComponent, ButtonComponent, InputComponent, ToggleComponent,
  NumberInputComponent, SelectComponent, ChipInputComponent,
  EmptyStateComponent, LoadingStateComponent,
  type SelectOption,
} from '@ui';
import { SeekerApi } from '@core/api/seeker.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { UpdateSeekerConfig } from '@shared/models/seeker-config.model';
import { HasPendingChanges } from '@core/guards/pending-changes.guard';
import { ApiError } from '@core/interceptors/error.interceptor';
import { DeferredLoader } from '@shared/utils/loading.util';
import { ScheduleUnit, SelectionStrategy } from '@shared/models/enums';
import { ScheduleOptions, generateCronExpression, parseCronToJobSchedule } from '@shared/utils/schedule.util';

const STRATEGY_OPTIONS: SelectOption[] = [
  { label: 'Balanced Weighted', value: SelectionStrategy.BalancedWeighted },
  { label: 'Oldest Search First', value: SelectionStrategy.OldestSearchFirst },
  { label: 'Oldest Search Weighted', value: SelectionStrategy.OldestSearchWeighted },
  { label: 'Newest First', value: SelectionStrategy.NewestFirst },
  { label: 'Newest Weighted', value: SelectionStrategy.NewestWeighted },
  { label: 'Random', value: SelectionStrategy.Random },
];

const STRATEGY_DESCRIPTIONS: Record<SelectionStrategy, string> = {
  [SelectionStrategy.BalancedWeighted]: 'Prioritizes items that are both newly added and haven\'t been searched recently. Good default for most libraries.',
  [SelectionStrategy.OldestSearchFirst]: 'Works through your library in order, starting with items that haven\'t been searched the longest. Guarantees every item gets covered.',
  [SelectionStrategy.OldestSearchWeighted]: 'Favors items that haven\'t been searched recently, but still gives other items a chance.',
  [SelectionStrategy.NewestFirst]: 'Always picks the most recently added items first. Best for keeping new additions up to date quickly.',
  [SelectionStrategy.NewestWeighted]: 'Favors recently added items, but still gives older items a chance.',
  [SelectionStrategy.Random]: 'Every item has an equal chance of being picked. No prioritization.',
};

const SCHEDULE_UNIT_OPTIONS: SelectOption[] = [
  { label: 'Seconds', value: ScheduleUnit.Seconds },
  { label: 'Minutes', value: ScheduleUnit.Minutes },
  { label: 'Hours', value: ScheduleUnit.Hours },
];

interface InstanceState {
  arrInstanceId: string;
  instanceName: string;
  instanceType: string;
  enabled: boolean;
  skipTags: string[];
  lastProcessedAt?: string;
}

@Component({
  selector: 'app-seeker',
  standalone: true,
  imports: [
    PageHeaderComponent, CardComponent, ButtonComponent, InputComponent,
    ToggleComponent, NumberInputComponent, SelectComponent, ChipInputComponent,
    EmptyStateComponent, LoadingStateComponent, DatePipe,
  ],
  templateUrl: './seeker.component.html',
  styleUrl: './seeker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeekerComponent implements OnInit, HasPendingChanges {
  private readonly api = inject(SeekerApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);

  private readonly savedSnapshot = signal('');

  readonly strategyOptions = STRATEGY_OPTIONS;
  readonly scheduleUnitOptions = SCHEDULE_UNIT_OPTIONS;
  readonly loader = new DeferredLoader();
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly enabled = signal(false);
  readonly useAdvancedScheduling = signal(false);
  readonly cronExpression = signal('');
  readonly scheduleEvery = signal<unknown>(1);
  readonly scheduleUnit = signal<unknown>(ScheduleUnit.Hours);
  readonly selectionStrategy = signal<unknown>(SelectionStrategy.BalancedWeighted);
  readonly monitoredOnly = signal(true);
  readonly useCutoff = signal(false);
  readonly useRoundRobin = signal(true);

  readonly instances = signal<InstanceState[]>([]);

  readonly strategyDescription = computed(() => STRATEGY_DESCRIPTIONS[this.selectionStrategy() as SelectionStrategy] ?? '');

  readonly scheduleIntervalOptions = computed(() => {
    const unit = this.scheduleUnit() as ScheduleUnit;
    const values = ScheduleOptions[unit] ?? [];
    return values.map(v => ({ label: `${v}`, value: v }));
  });

  constructor() {
    effect(() => {
      const unit = this.scheduleUnit();
      const options = ScheduleOptions[unit as ScheduleUnit] ?? [];
      const current = this.scheduleEvery();
      if (options.length > 0 && !options.includes(current as number)) {
        untracked(() => this.scheduleEvery.set(options[0]));
      }
    });
  }

  // Validation
  readonly scheduleEveryError = computed(() => {
    if (this.useAdvancedScheduling()) return undefined;
    const unit = this.scheduleUnit() as ScheduleUnit;
    const options = ScheduleOptions[unit] ?? [];
    if (!options.includes(this.scheduleEvery() as number)) return 'Please select a value';
    return undefined;
  });

  readonly cronError = computed(() => {
    if (this.useAdvancedScheduling() && !this.cronExpression().trim()) return 'Cron expression is required';
    return undefined;
  });

  readonly instanceError = computed(() => {
    if (this.enabled() && this.instances().length > 0 && !this.instances().some(i => i.enabled)) {
      return 'At least one instance must be enabled';
    }
    return undefined;
  });

  readonly hasErrors = computed(() => !!(
    this.scheduleEveryError() ||
    this.cronError() ||
    this.instanceError()
  ));

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.loader.start();
    this.api.getConfig().subscribe({
      next: (config) => {
        this.enabled.set(config.enabled);
        this.useAdvancedScheduling.set(config.useAdvancedScheduling);
        this.cronExpression.set(config.cronExpression);
        const parsed = parseCronToJobSchedule(config.cronExpression);
        if (parsed) {
          this.scheduleEvery.set(parsed.every);
          this.scheduleUnit.set(parsed.type);
        }
        this.selectionStrategy.set(config.selectionStrategy);
        this.monitoredOnly.set(config.monitoredOnly);
        this.useCutoff.set(config.useCutoff);
        this.useRoundRobin.set(config.useRoundRobin);
        this.instances.set(config.instances.map(i => ({
          arrInstanceId: i.arrInstanceId,
          instanceName: i.instanceName,
          instanceType: i.instanceType,
          enabled: i.enabled,
          skipTags: [...i.skipTags],
          lastProcessedAt: i.lastProcessedAt,
        })));
        this.loader.stop();
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: () => {
        this.toast.error('Failed to load seeker settings');
        this.loader.stop();
        this.loadError.set(true);
      },
    });
  }

  retry(): void {
    this.loadError.set(false);
    this.loadConfig();
  }

  async toggleRoundRobin(newValue: boolean): Promise<void> {
    if (!newValue) {
      const confirmed = await this.confirm.confirm({
        title: 'Disable Round Robin',
        message: 'Disabling round robin will trigger a search for each enabled arr instance per run. This could result in too many requests to your indexers and potentially get you banned.',
        confirmLabel: 'Disable',
        destructive: true,
      });
      if (!confirmed) {
        // The toggle already flipped its internal state to false.
        // Sync our signal to false first, then restore to true in the next tick
        // so Angular detects an actual change and pushes it back to the toggle.
        this.useRoundRobin.set(false);
        setTimeout(() => this.useRoundRobin.set(true));
        return;
      }
    }
    this.useRoundRobin.set(newValue);
  }

  toggleInstance(index: number): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], enabled: !updated[index].enabled };
      return updated;
    });
  }

  updateInstanceSkipTags(index: number, tags: string[]): void {
    this.instances.update(instances => {
      const updated = [...instances];
      updated[index] = { ...updated[index], skipTags: tags };
      return updated;
    });
  }

  getInstanceIcon(instanceType: string): string {
    return `icons/ext/${instanceType.toLowerCase()}-light.svg`;
  }

  save(): void {
    const jobSchedule = { every: (this.scheduleEvery() as number) ?? 1, type: this.scheduleUnit() as ScheduleUnit };
    const cronExpression = this.useAdvancedScheduling()
      ? this.cronExpression()
      : generateCronExpression(jobSchedule);

    const config: UpdateSeekerConfig = {
      enabled: this.enabled(),
      cronExpression,
      useAdvancedScheduling: this.useAdvancedScheduling(),
      selectionStrategy: this.selectionStrategy() as SelectionStrategy,
      monitoredOnly: this.monitoredOnly(),
      useCutoff: this.useCutoff(),
      useRoundRobin: this.useRoundRobin(),
      instances: this.instances().map(i => ({
        arrInstanceId: i.arrInstanceId,
        enabled: i.enabled,
        skipTags: i.skipTags,
      })),
    };

    this.saving.set(true);
    this.api.updateConfig(config).subscribe({
      next: () => {
        this.toast.success('Seeker settings saved');
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 1500);
        this.savedSnapshot.set(this.buildSnapshot());
      },
      error: (err: ApiError) => {
        this.toast.error(err.statusCode === 400
          ? err.message
          : 'Failed to save seeker settings');
        this.saving.set(false);
      },
    });
  }

  private buildSnapshot(): string {
    return JSON.stringify({
      enabled: this.enabled(),
      useAdvancedScheduling: this.useAdvancedScheduling(),
      cronExpression: this.cronExpression(),
      scheduleEvery: this.scheduleEvery(),
      scheduleUnit: this.scheduleUnit(),
      selectionStrategy: this.selectionStrategy(),
      monitoredOnly: this.monitoredOnly(),
      useCutoff: this.useCutoff(),
      useRoundRobin: this.useRoundRobin(),
      instances: this.instances(),
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
