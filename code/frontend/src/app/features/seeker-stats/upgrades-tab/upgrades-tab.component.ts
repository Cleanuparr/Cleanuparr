import { Component, ChangeDetectionStrategy, inject, signal, effect, untracked, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon } from '@ng-icons/core';
import {
  CardComponent, BadgeComponent, ButtonComponent, SelectComponent,
  PaginatorComponent, EmptyStateComponent,
} from '@ui';
import type { SelectOption } from '@ui';
import { AnimatedCounterComponent } from '@ui/animated-counter/animated-counter.component';
import { CfScoreApi, CfScoreUpgrade } from '@core/api/cf-score.api';
import { AppHubService } from '@core/realtime/app-hub.service';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-upgrades-tab',
  standalone: true,
  imports: [
    DatePipe,
    NgIcon,
    CardComponent,
    BadgeComponent,
    ButtonComponent,
    SelectComponent,
    PaginatorComponent,
    EmptyStateComponent,
    AnimatedCounterComponent,
  ],
  templateUrl: './upgrades-tab.component.html',
  styleUrl: './upgrades-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpgradesTabComponent implements OnInit {
  private readonly api = inject(CfScoreApi);
  private readonly hub = inject(AppHubService);
  private readonly toast = inject(ToastService);
  private initialLoad = true;

  readonly upgrades = signal<CfScoreUpgrade[]>([]);
  readonly totalRecords = signal(0);
  readonly currentPage = signal(1);
  readonly pageSize = signal(50);
  readonly loading = signal(false);

  readonly timeRange = signal<string>('30');
  readonly selectedInstanceId = signal<string>('');
  readonly instanceOptions = signal<SelectOption[]>([]);

  readonly timeRangeOptions: SelectOption[] = [
    { label: 'Last 7 Days', value: '7' },
    { label: 'Last 30 Days', value: '30' },
    { label: 'Last 90 Days', value: '90' },
    { label: 'All Time', value: '0' },
  ];

  constructor() {
    effect(() => {
      this.hub.cfScoresVersion();
      if (this.initialLoad) {
        this.initialLoad = false;
        return;
      }
      untracked(() => {
        this.loadUpgrades();
      });
    });
  }

  ngOnInit(): void {
    this.loadInstances();
    this.loadUpgrades();
  }

  onTimeRangeChange(value: string): void {
    this.timeRange.set(value);
    this.currentPage.set(1);
    this.loadUpgrades();
  }

  onInstanceFilterChange(value: string): void {
    this.selectedInstanceId.set(value);
    this.currentPage.set(1);
    this.loadUpgrades();
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadUpgrades();
  }

  refresh(): void {
    this.loadUpgrades();
  }

  itemTypeSeverity(itemType: string): 'info' | 'default' {
    return itemType === 'Radarr' || itemType === 'Sonarr' ? 'info' : 'default';
  }

  private loadInstances(): void {
    this.api.getInstances().subscribe({
      next: (result) => {
        this.instanceOptions.set([
          { label: 'All Instances', value: '' },
          ...result.instances.map(i => ({
            label: `${i.name} (${i.itemType})`,
            value: i.id,
          })),
        ]);
      },
      error: () => this.toast.error('Failed to load instances'),
    });
  }

  private loadUpgrades(): void {
    this.loading.set(true);
    const days = parseInt(this.timeRange(), 10) || undefined;
    const instanceId = this.selectedInstanceId() || undefined;

    this.api.getRecentUpgrades(this.currentPage(), this.pageSize(), instanceId, days).subscribe({
      next: (result) => {
        this.upgrades.set(result.items);
        this.totalRecords.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load upgrades');
      },
    });
  }
}
