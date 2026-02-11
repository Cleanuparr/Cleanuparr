import { Component, ChangeDetectionStrategy, input, output, inject } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { ThemeService } from '@core/services/theme.service';
import { ToggleComponent } from '@ui';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [NgIcon, ToggleComponent],
  templateUrl: './toolbar.component.html',
  styleUrl: './toolbar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToolbarComponent {
  sidebarCollapsed = input(false);
  toggleSidebar = output<void>();

  private themeService = inject(ThemeService);

  theme = this.themeService.theme;
  performanceMode = this.themeService.performanceMode;

  onToggleTheme(): void {
    this.themeService.toggleTheme();
  }

  onTogglePerformanceMode(): void {
    this.themeService.togglePerformanceMode();
  }

  onToggleSidebar(): void {
    this.toggleSidebar.emit();
  }
}
