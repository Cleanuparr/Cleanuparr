import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'dark' | 'light';

export const ACCENT_PRESETS = [
  'default',
  'blue',
  'green',
  'rose',
  'amber',
  'teal',
] as const;
export type AccentPreset = (typeof ACCENT_PRESETS)[number];
export type Accent = AccentPreset | 'custom';

const THEME_KEY = 'cleanuparr-theme';
const PERFORMANCE_MODE_KEY = 'cleanuparr-performance-mode';
const FULL_WIDTH_KEY = 'cleanuparr-full-width';
const ACCENT_KEY = 'cleanuparr-accent';
const CUSTOM_ACCENT_KEY = 'cleanuparr-custom-accent';

const DEFAULT_CUSTOM_ACCENT = '#8b5cf6';
const HEX_COLOR_REGEX = /^#[0-9a-f]{6}$/i;
const BRAND_SHADES = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950] as const;

// Lightness stops per shade, tuned to match the visual weight of the default purple scale.
const LIGHTNESS_STOPS: Record<(typeof BRAND_SHADES)[number], number> = {
  50: 97,
  100: 93,
  200: 86,
  300: 75,
  400: 62,
  500: 50,
  600: 42,
  700: 34,
  800: 27,
  900: 20,
  950: 12,
};

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>('dark');
  private readonly _performanceMode = signal(false);
  private readonly _fullWidth = signal(false);
  private readonly _accent = signal<Accent>('default');
  private readonly _customAccent = signal<string>(DEFAULT_CUSTOM_ACCENT);

  readonly theme = this._theme.asReadonly();
  readonly performanceMode = this._performanceMode.asReadonly();
  readonly fullWidth = this._fullWidth.asReadonly();
  readonly accent = this._accent.asReadonly();
  readonly customAccent = this._customAccent.asReadonly();

  constructor() {
    this.restoreFromStorage();
    this.detectSystemPreferences();
    this.bindToDom();
  }

  toggleTheme(): void {
    const next = this._theme() === 'dark' ? 'light' : 'dark';
    this._theme.set(next);
    localStorage.setItem(THEME_KEY, next);
  }

  setTheme(theme: Theme): void {
    this._theme.set(theme);
    localStorage.setItem(THEME_KEY, theme);
  }

  togglePerformanceMode(): void {
    const next = !this._performanceMode();
    this._performanceMode.set(next);
    localStorage.setItem(PERFORMANCE_MODE_KEY, String(next));
  }

  setPerformanceMode(value: boolean): void {
    this._performanceMode.set(value);
    localStorage.setItem(PERFORMANCE_MODE_KEY, String(value));
  }

  toggleFullWidth(): void {
    const next = !this._fullWidth();
    this._fullWidth.set(next);
    localStorage.setItem(FULL_WIDTH_KEY, String(next));
  }

  setFullWidth(value: boolean): void {
    this._fullWidth.set(value);
    localStorage.setItem(FULL_WIDTH_KEY, String(value));
  }

  setAccent(accent: Accent): void {
    this._accent.set(accent);
    localStorage.setItem(ACCENT_KEY, accent);
  }

  setCustomAccent(hex: string): void {
    const normalized = hex.trim().toLowerCase();
    if (!HEX_COLOR_REGEX.test(normalized))
    {
      return;
    }
    this._customAccent.set(normalized);
    localStorage.setItem(CUSTOM_ACCENT_KEY, normalized);
    if (this._accent() !== 'custom')
    {
      this.setAccent('custom');
    }
  }

  private restoreFromStorage(): void {
    const savedTheme = localStorage.getItem(THEME_KEY);
    if (savedTheme === 'light' || savedTheme === 'dark') {
      this._theme.set(savedTheme);
    }

    const saved = localStorage.getItem(PERFORMANCE_MODE_KEY);
    if (saved === 'true') {
      this._performanceMode.set(true);
    }

    const savedFullWidth = localStorage.getItem(FULL_WIDTH_KEY);
    if (savedFullWidth === 'true') {
      this._fullWidth.set(true);
    }

    const savedAccent = localStorage.getItem(ACCENT_KEY);
    const migratedAccent = savedAccent === 'purple' ? 'default' : savedAccent;
    if (migratedAccent && this.isAccent(migratedAccent)) {
      this._accent.set(migratedAccent);
    }

    const savedCustom = localStorage.getItem(CUSTOM_ACCENT_KEY);
    if (savedCustom && HEX_COLOR_REGEX.test(savedCustom))
    {
      this._customAccent.set(savedCustom);
    }
  }

  private detectSystemPreferences(): void {
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (prefersReducedMotion.matches && localStorage.getItem(PERFORMANCE_MODE_KEY) === null) {
      this._performanceMode.set(true);
    }
  }

  private bindToDom(): void {
    const root = document.documentElement;

    effect(() => {
      root.setAttribute('data-theme', this._theme());
    });

    effect(() => {
      root.setAttribute('data-performance-mode', String(this._performanceMode()));
    });

    effect(() => {
      root.setAttribute('data-full-width', String(this._fullWidth()));
    });

    effect(() => {
      const accent = this._accent();
      root.setAttribute('data-accent', accent);

      if (accent === 'custom') {
        this.applyCustomAccent(this._customAccent());
      } else {
        this.clearInlineAccent();
      }
    });
  }

  private isAccent(value: string): value is Accent {
    return value === 'custom' || (ACCENT_PRESETS as readonly string[]).includes(value);
  }

  private applyCustomAccent(hex: string): void {
    const root = document.documentElement;
    const rgb = hexToRgb(hex);
    if (!rgb)
    {
      return;
    }

    const hsl = rgbToHsl(rgb.r, rgb.g, rgb.b);
    // Keep some chroma even when the user picks a near-gray, otherwise the whole
    // brand scale collapses to shades of gray and active states become invisible.
    const s = Math.max(hsl.s, 15);

    for (const shade of BRAND_SHADES)
    {
      const l = shade === 500 ? hsl.l : LIGHTNESS_STOPS[shade];
      const { r, g, b } = hslToRgb(hsl.h, s, l);
      root.style.setProperty(`--brand-${shade}`, rgbToHex(r, g, b));
    }

    root.style.setProperty('--accent-rgb', `${rgb.r}, ${rgb.g}, ${rgb.b}`);
  }

  private clearInlineAccent(): void {
    const root = document.documentElement;
    for (const shade of BRAND_SHADES) {
      root.style.removeProperty(`--brand-${shade}`);
    }
    root.style.removeProperty('--accent-rgb');
  }
}

function hexToRgb(hex: string): { r: number; g: number; b: number } | null {
  const match = /^#([0-9a-f]{6})$/i.exec(hex.trim());
  if (!match) return null;
  const n = parseInt(match[1], 16);
  return { r: (n >> 16) & 0xff, g: (n >> 8) & 0xff, b: n & 0xff };
}

function rgbToHex(r: number, g: number, b: number): string {
  const clamp = (v: number) => Math.max(0, Math.min(255, Math.round(v)));
  const hex = (v: number) => clamp(v).toString(16).padStart(2, '0');
  return `#${hex(r)}${hex(g)}${hex(b)}`;
}

function rgbToHsl(r: number, g: number, b: number): { h: number; s: number; l: number } {
  const rn = r / 255, gn = g / 255, bn = b / 255;
  const max = Math.max(rn, gn, bn);
  const min = Math.min(rn, gn, bn);
  const l = (max + min) / 2;
  let h = 0, s = 0;
  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case rn: h = (gn - bn) / d + (gn < bn ? 6 : 0); break;
      case gn: h = (bn - rn) / d + 2; break;
      case bn: h = (rn - gn) / d + 4; break;
    }
    h *= 60;
  }
  return { h, s: s * 100, l: l * 100 };
}

function hslToRgb(h: number, s: number, l: number): { r: number; g: number; b: number } {
  const sn = s / 100, ln = l / 100;
  const c = (1 - Math.abs(2 * ln - 1)) * sn;
  const hp = h / 60;
  const x = c * (1 - Math.abs((hp % 2) - 1));
  let r1 = 0, g1 = 0, b1 = 0;
  if (hp >= 0 && hp < 1) [r1, g1, b1] = [c, x, 0];
  else if (hp < 2) [r1, g1, b1] = [x, c, 0];
  else if (hp < 3) [r1, g1, b1] = [0, c, x];
  else if (hp < 4) [r1, g1, b1] = [0, x, c];
  else if (hp < 5) [r1, g1, b1] = [x, 0, c];
  else [r1, g1, b1] = [c, 0, x];
  const m = ln - c / 2;
  return { r: (r1 + m) * 255, g: (g1 + m) * 255, b: (b1 + m) * 255 };
}
