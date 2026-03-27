import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { TabsComponent } from '@ui';
import type { Tab } from '@ui';
import { SearchesTabComponent } from './searches-tab/searches-tab.component';
import { QualityTabComponent } from './quality-tab/quality-tab.component';
import { UpgradesTabComponent } from './upgrades-tab/upgrades-tab.component';

type SeekerTab = 'searches' | 'quality' | 'upgrades';

@Component({
  selector: 'app-seeker-stats',
  standalone: true,
  imports: [
    PageHeaderComponent,
    TabsComponent,
    SearchesTabComponent,
    QualityTabComponent,
    UpgradesTabComponent,
  ],
  templateUrl: './seeker-stats.component.html',
  styleUrl: './seeker-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeekerStatsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly activeTab = signal<string>('searches');

  readonly tabs: Tab[] = [
    { id: 'searches', label: 'Searches' },
    { id: 'quality', label: 'Quality Scores' },
    { id: 'upgrades', label: 'Upgrades' },
  ];

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab && ['searches', 'quality', 'upgrades'].includes(tab)) {
      this.activeTab.set(tab);
    }
  }

  onTabChange(tabId: string): void {
    this.activeTab.set(tabId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: tabId },
      queryParamsHandling: 'merge',
    });
  }
}
