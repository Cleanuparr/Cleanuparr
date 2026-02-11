import { Component, ChangeDetectionStrategy, input, model } from '@angular/core';

export interface Tab {
  id: string;
  label: string;
  disabled?: boolean;
}

@Component({
  selector: 'app-tabs',
  standalone: true,
  templateUrl: './tabs.component.html',
  styleUrl: './tabs.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TabsComponent {
  tabs = input<Tab[]>([]);
  activeTab = model<string>('');

  selectTab(tab: Tab): void {
    if (!tab.disabled) {
      this.activeTab.set(tab.id);
    }
  }
}
