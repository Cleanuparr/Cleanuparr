import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'dark' | 'light';

const THEME_KEY = 'cleanuparr-theme';
const PERFORMANCE_MODE_KEY = 'cleanuparr-performance-mode';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>('dark');
  private readonly _performanceMode = signal(false);

  readonly theme = this._theme.asReadonly();
  readonly performanceMode = this._performanceMode.asReadonly();

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

  private restoreFromStorage(): void {
    const savedTheme = localStorage.getItem(THEME_KEY);
    if (savedTheme === 'light' || savedTheme === 'dark') {
      this._theme.set(savedTheme);
    }

    const saved = localStorage.getItem(PERFORMANCE_MODE_KEY);
    if (saved === 'true') {
      this._performanceMode.set(true);
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
  }
}
