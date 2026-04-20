import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'dark' | 'light';

const THEME_KEY = 'cleanuparr-theme';
const PERFORMANCE_MODE_KEY = 'cleanuparr-performance-mode';
const FULL_WIDTH_KEY = 'cleanuparr-full-width';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>('dark');
  private readonly _performanceMode = signal(false);
  private readonly _fullWidth = signal(false);

  readonly theme = this._theme.asReadonly();
  readonly performanceMode = this._performanceMode.asReadonly();
  readonly fullWidth = this._fullWidth.asReadonly();

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
  }

  private detectSystemPreferences(): void {
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (prefersReducedMotion.matches && localStorage.getItem(PERFORMANCE_MODE_KEY) === null) {
      this._performanceMode.set(true);
    }
  }

  private bindToDom(): void {
    effect(() => {
      document.documentElement.setAttribute('data-theme', this._theme());
    });

    effect(() => {
      document.documentElement.setAttribute('data-performance-mode', String(this._performanceMode()));
    });

    effect(() => {
      document.documentElement.setAttribute('data-full-width', String(this._fullWidth()));
    });
  }
}
