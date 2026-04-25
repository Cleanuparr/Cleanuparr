import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { PageHeaderComponent } from '@layout/page-header/page-header.component';
import { CardComponent } from '@ui';
import {
  ACCENT_PRESETS,
  ACCENT_PRESET_HEX,
  Accent,
  Theme,
  ThemeService,
} from '@core/services/theme.service';

interface AccentSwatch {
  readonly value: Accent;
  readonly label: string;
  readonly color: string;
}

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
    color: ACCENT_PRESET_HEX[value],
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
