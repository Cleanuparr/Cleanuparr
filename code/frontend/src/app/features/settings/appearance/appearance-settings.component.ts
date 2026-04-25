import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent } from '@ui';
import {
  ACCENT_PRESETS,
  Accent,
  AccentPreset,
  Theme,
  ThemeService,
} from '@core/services/theme.service';

interface AccentSwatch {
  readonly value: Accent;
  readonly label: string;
  readonly color: string;
}

const PRESET_SWATCHES: Record<AccentPreset, string> = {
  default: '#8b5cf6',
  blue: '#3b82f6',
  green: '#10b981',
  rose: '#f43f5e',
  amber: '#f59e0b',
  teal: '#14b8a6',
};

@Component({
  selector: 'app-appearance-settings',
  standalone: true,
  imports: [PageHeaderComponent, CardComponent],
  templateUrl: './appearance-settings.component.html',
  styleUrl: './appearance-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppearanceSettingsComponent {
  private readonly themeService = inject(ThemeService);

  readonly theme = this.themeService.theme;
  readonly accent = this.themeService.accent;
  readonly customAccent = this.themeService.customAccent;

  readonly presetSwatches: AccentSwatch[] = ACCENT_PRESETS.map((value) => ({
    value,
    label: value.charAt(0).toUpperCase() + value.slice(1),
    color: PRESET_SWATCHES[value],
  }));

  selectTheme(theme: Theme): void {
    this.themeService.setTheme(theme);
  }

  selectAccent(accent: Accent): void {
    this.themeService.setAccent(accent);
  }

  onCustomColorChange(event: Event): void {
    const { value } = event.target as HTMLInputElement;
    this.themeService.setCustomAccent(value);
  }
}
