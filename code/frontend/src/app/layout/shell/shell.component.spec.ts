import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { FeatureBadgeService } from '@core/feature-badges/feature-badge.service';
import { AppHubService } from '@core/realtime/app-hub.service';
import { OverlayStackService } from '@core/services/overlay-stack.service';
import { ShellComponent } from './shell.component';

interface Harness {
  fixture: ComponentFixture<ShellComponent>;
  hubCalls: string[];
}

const DESKTOP_WIDTH = 1400;

describe('ShellComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    setWidth(1024);
  });

  function setWidth(width: number): void {
    Object.defineProperty(window, 'innerWidth', { value: width, configurable: true, writable: true });
  }

  function setup(width = DESKTOP_WIDTH): Harness {
    vi.stubGlobal('matchMedia', () => ({ matches: false }));
    setWidth(width);
    const hubCalls: string[] = [];

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        {
          provide: AppHubService,
          useValue: {
            appStatus: signal(null),
            start: () => hubCalls.push('start'),
            stop: () => hubCalls.push('stop'),
          },
        },
        { provide: AuthService, useValue: { logout: () => undefined } },
        { provide: FeatureBadgeService, useValue: { init: () => hubCalls.push('badges') } },
      ],
    });

    const fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
    return { fixture, hubCalls };
  }

  function backdrop(fixture: ComponentFixture<ShellComponent>): HTMLElement | null {
    return fixture.nativeElement.querySelector('.shell__backdrop') as HTMLElement | null;
  }

  it('opens the mobile menu on toggle and closes it from the backdrop', () => {
    const { fixture } = setup(600);
    const component = fixture.componentInstance;

    expect(component.isMobile()).toBe(true);
    expect(backdrop(fixture)).toBeNull();

    component.toggleSidebar();
    fixture.detectChanges();

    expect(component.mobileMenuOpen()).toBe(true);
    expect(component.sidebarCollapsed()).toBe(false);
    expect(backdrop(fixture)).not.toBeNull();

    backdrop(fixture)!.click();
    fixture.detectChanges();

    expect(component.mobileMenuOpen()).toBe(false);
    expect(backdrop(fixture)).toBeNull();
  });

  it('collapses the sidebar instead of opening a menu on a wide viewport', () => {
    const { fixture, hubCalls } = setup();
    const component = fixture.componentInstance;

    expect(hubCalls).toEqual(['start', 'badges']);
    expect(component.isMobile()).toBe(false);
    expect(component.sidebarCollapsed()).toBe(false);

    component.toggleSidebar();
    fixture.detectChanges();

    expect(component.sidebarCollapsed()).toBe(true);
    expect(component.mobileMenuOpen()).toBe(false);
    expect((fixture.nativeElement.querySelector('.shell') as HTMLElement).classList).toContain('shell--collapsed');
  });

  it('auto collapses at tablet width and restores the sidebar when the window widens again', () => {
    const { fixture } = setup(900);
    const component = fixture.componentInstance;

    expect(component.isMobile()).toBe(false);
    expect(component.sidebarCollapsed()).toBe(true);

    setWidth(DESKTOP_WIDTH);
    component.onResize();
    fixture.detectChanges();

    expect(component.sidebarCollapsed()).toBe(false);
  });

  it('keeps a manual collapse when the window is resized back to desktop', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.toggleSidebar();
    setWidth(DESKTOP_WIDTH);
    component.onResize();
    fixture.detectChanges();

    expect(component.sidebarCollapsed()).toBe(true);
  });

  it('registers the open mobile menu on the overlay stack and releases it on close', () => {
    const { fixture } = setup(600);
    const overlays = TestBed.inject(OverlayStackService);
    const other = overlays.register();

    expect(overlays.isTopmost(other)).toBe(true);

    fixture.componentInstance.toggleSidebar();
    fixture.detectChanges();
    expect(overlays.isTopmost(other)).toBe(false);

    fixture.componentInstance.closeMobileMenu();
    fixture.detectChanges();
    expect(overlays.isTopmost(other)).toBe(true);

    overlays.unregister(other);
  });

  it('dismisses the mobile menu with escape only while it is the topmost overlay', () => {
    const { fixture } = setup(600);
    const overlays = TestBed.inject(OverlayStackService);
    const component = fixture.componentInstance;

    component.toggleSidebar();
    fixture.detectChanges();

    const above = overlays.register();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(component.mobileMenuOpen()).toBe(true);

    overlays.unregister(above);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(component.mobileMenuOpen()).toBe(false);
  });

  it('closes the mobile menu on navigation and stops the hub on destroy', async () => {
    const { fixture, hubCalls } = setup(600);
    const component = fixture.componentInstance;

    component.toggleSidebar();
    fixture.detectChanges();
    expect(component.mobileMenuOpen()).toBe(true);

    await TestBed.inject(Router).navigateByUrl('/logs');
    fixture.detectChanges();

    expect(component.mobileMenuOpen()).toBe(false);

    fixture.destroy();
    expect(hubCalls).toContain('stop');
  });
});
