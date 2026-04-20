import { Component, ChangeDetectionStrategy, input, output, inject } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { ThemeService } from '@core/services/theme.service';
import { ToggleComponent, TooltipComponent } from '@ui';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [NgIcon, ToggleComponent, TooltipComponent],
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
  fullWidth = this.themeService.fullWidth;

  onToggleTheme(): void {
    this.themeService.toggleTheme();
  }

  onTogglePerformanceMode(): void {
    this.themeService.togglePerformanceMode();
  }

  onToggleFullWidth(): void {
    this.themeService.toggleFullWidth();
  }

  onToggleSidebar(): void {
    this.toggleSidebar.emit();
  }
}
