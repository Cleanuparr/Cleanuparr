import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

const BRAND_SHADES = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950] as const;
const DATA_ATTRIBUTES = ['data-theme', 'data-performance-mode', 'data-full-width', 'data-accent'];

describe('ThemeService', () => {
  const root = document.documentElement;

  function setup(): ThemeService {
    const service = TestBed.inject(ThemeService);
    TestBed.tick();
    return service;
  }

  function brandStop(shade: number): string {
    return root.style.getPropertyValue(`--brand-${shade}`).trim();
  }

  function channels(hex: string): [number, number, number] {
    const value = parseInt(hex.slice(1), 16);
    return [(value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff];
  }

  function luminance(hex: string): number {
    const [r, g, b] = channels(hex);
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  }

  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal('matchMedia', () => ({ matches: false }));
  });

  afterEach(() => {
    localStorage.clear();
    vi.unstubAllGlobals();
    for (const attribute of DATA_ATTRIBUTES) {
      root.removeAttribute(attribute);
    }
    for (const shade of BRAND_SHADES) {
      root.style.removeProperty(`--brand-${shade}`);
    }
    root.style.removeProperty('--accent-rgb');
  });

  it('round-trips the picked hex back through the mid brand stop and publishes its rgb triple', () => {
    const service = setup();

    service.setCustomAccent('#3B82F6');
    TestBed.tick();

    expect(service.customAccent()).toBe('#3b82f6');
    expect(brandStop(500)).toBe('#3b82f6');
    expect(root.style.getPropertyValue('--accent-rgb').trim()).toBe('59, 130, 246');
  });

  it('generates every brand stop, darkening from 50 down to 950', () => {
    const service = setup();

    service.setCustomAccent('#3b82f6');
    TestBed.tick();

    const stops = BRAND_SHADES.map((shade) => brandStop(shade));
    expect(stops.every((stop) => /^#[0-9a-f]{6}$/.test(stop))).toBe(true);

    const luminances = stops.map((stop) => luminance(stop));
    for (let index = 1; index < luminances.length; index++) {
      expect(luminances[index]).toBeLessThan(luminances[index - 1]);
    }
  });

  it('keeps the ramp strictly ordered for accents lighter and darker than the mid stop', () => {
    const service = setup();

    for (const hex of ['#8b5cf6', '#10b981']) {
      service.setCustomAccent(hex);
      TestBed.tick();

      const luminances = BRAND_SHADES.map((shade) => luminance(brandStop(shade)));
      for (let index = 1; index < luminances.length; index++) {
        expect(luminances[index]).toBeLessThan(luminances[index - 1]);
      }
    }
  });

  it('never inverts the ramp even for a near-white or near-black accent', () => {
    const service = setup();

    for (const hex of ['#f8fafc', '#111827']) {
      service.setCustomAccent(hex);
      TestBed.tick();

      const luminances = BRAND_SHADES.map((shade) => luminance(brandStop(shade)));
      for (let index = 1; index < luminances.length; index++) {
        expect(luminances[index]).toBeLessThanOrEqual(luminances[index - 1]);
      }
    }
  });

  it('anchors the mid stop on the picked hex for a light and a dark accent', () => {
    const service = setup();

    for (const hex of ['#8b5cf6', '#10b981']) {
      service.setCustomAccent(hex);
      TestBed.tick();

      expect(brandStop(500)).toBe(hex);
    }
  });

  it('keeps chroma in the ramp when the picked color is a near-gray', () => {
    const service = setup();

    service.setCustomAccent('#808080');
    TestBed.tick();

    for (const shade of [300, 500, 700]) {
      const [r, g, b] = channels(brandStop(shade));
      expect(r === g && g === b).toBe(false);
    }
  });

  it('ignores hex values that are not six digits, unprefixed or non-hexadecimal', () => {
    const service = setup();

    for (const invalid of ['#fff', '3b82f6', '#gggggg', '#3b82f67', '']) {
      service.setCustomAccent(invalid);
    }
    TestBed.tick();

    expect(service.customAccent()).toBe('#8b5cf6');
    expect(service.accent()).toBe('default');
    expect(root.getAttribute('data-accent')).toBe('default');
    expect(brandStop(500)).toBe('');
    expect(localStorage.getItem('cleanuparr-custom-accent')).toBeNull();
  });

  it('switches to the custom accent and clears the inline ramp when a preset is picked again', () => {
    const service = setup();

    service.setCustomAccent('#f43f5e');
    TestBed.tick();

    expect(service.accent()).toBe('custom');
    expect(root.getAttribute('data-accent')).toBe('custom');
    expect(brandStop(500)).toBe('#f43f5e');

    service.setAccent('teal');
    TestBed.tick();

    expect(root.getAttribute('data-accent')).toBe('teal');
    expect(brandStop(500)).toBe('');
    expect(root.style.getPropertyValue('--accent-rgb')).toBe('');
    expect(service.customAccent()).toBe('#f43f5e');
  });

  it('restores the stored theme and migrates the legacy purple accent to default', () => {
    localStorage.setItem('cleanuparr-theme', 'light');
    localStorage.setItem('cleanuparr-accent', 'purple');

    const service = setup();

    expect(service.theme()).toBe('light');
    expect(root.getAttribute('data-theme')).toBe('light');
    expect(service.accent()).toBe('default');
  });

  it('falls back to the default accent when the stored accent or custom hex is unusable', () => {
    localStorage.setItem('cleanuparr-accent', 'chartreuse');
    localStorage.setItem('cleanuparr-custom-accent', 'not-a-color');

    const service = setup();

    expect(service.accent()).toBe('default');
    expect(service.customAccent()).toBe('#8b5cf6');
    expect(brandStop(500)).toBe('');
  });
});
