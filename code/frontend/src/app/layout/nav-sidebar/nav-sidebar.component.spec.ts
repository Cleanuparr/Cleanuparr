import { WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AppStatus } from '@core/models/app-status.model';
import { AppHubService } from '@core/realtime/app-hub.service';
import { AuthService } from '@core/auth/auth.service';
import { NavSidebarComponent } from './nav-sidebar.component';

interface Harness {
  fixture: ComponentFixture<NavSidebarComponent>;
  appStatus: WritableSignal<AppStatus | null>;
  logouts: number[];
}

describe('NavSidebarComponent', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function setup(status: AppStatus | null = null): Harness {
    vi.stubGlobal('matchMedia', () => ({ matches: false }));
    const appStatus = signal(status);
    const logouts: number[] = [];

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        { provide: AppHubService, useValue: { appStatus } },
        { provide: AuthService, useValue: { logout: () => logouts.push(1) } },
      ],
    });

    const fixture = TestBed.createComponent(NavSidebarComponent);
    fixture.detectChanges();
    return { fixture, appStatus, logouts };
  }

  function link(fixture: ComponentFixture<NavSidebarComponent>, route: string): HTMLAnchorElement {
    return fixture.nativeElement.querySelector(`a[href="${route}"]`) as HTMLAnchorElement;
  }

  it('marks only the link of the current route as active', async () => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/settings/general');
    fixture.detectChanges();

    expect(link(fixture, '/settings/general').classList).toContain('sidebar__item--active');
    expect(link(fixture, '/dashboard').classList).not.toContain('sidebar__item--active');

    await router.navigateByUrl('/dashboard');
    fixture.detectChanges();

    expect(link(fixture, '/dashboard').classList).toContain('sidebar__item--active');
    expect(link(fixture, '/settings/general').classList).not.toContain('sidebar__item--active');
  });

  it('flags an update only when both versions are known and differ', () => {
    const { fixture, appStatus } = setup();
    const component = fixture.componentInstance;

    expect(component.hasUpdate()).toBe(false);

    appStatus.set({ currentVersion: '2.0.0', latestVersion: null });
    expect(component.hasUpdate()).toBe(false);

    appStatus.set({ currentVersion: null, latestVersion: '2.1.0' });
    expect(component.hasUpdate()).toBe(false);

    appStatus.set({ currentVersion: '2.1.0', latestVersion: '2.1.0' });
    expect(component.hasUpdate()).toBe(false);

    appStatus.set({ currentVersion: '2.0.0', latestVersion: '2.1.0' });
    expect(component.hasUpdate()).toBe(true);
  });

  it('shows the version and update links only when an update is available', () => {
    const { fixture, appStatus } = setup({ currentVersion: '2.0.0', latestVersion: '2.0.0' });

    expect(fixture.nativeElement.querySelector('.sidebar__version')!.textContent).toContain('2.0.0');
    expect(fixture.nativeElement.querySelector('.sidebar__update')).toBeNull();

    appStatus.set({ currentVersion: '2.0.0', latestVersion: '2.1.0' });
    fixture.detectChanges();

    const update = fixture.nativeElement.querySelector('.sidebar__update') as HTMLAnchorElement;
    expect(update.textContent).toContain('Update available: 2.1.0');
    expect(update.getAttribute('href')).toBe('https://github.com/Cleanuparr/Cleanuparr/releases/2.1.0');
  });

  it('drops the section toggles and the version link while collapsed but keeps the nav items', () => {
    const { fixture } = setup({ currentVersion: '2.0.0', latestVersion: '2.1.0' });

    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.sidebar__section-toggle')).toHaveLength(0);
    expect(fixture.nativeElement.querySelector('.sidebar__version')).toBeNull();
    expect(fixture.nativeElement.querySelector('.sidebar__update')).toBeNull();
    expect(link(fixture, '/settings/general')).not.toBeNull();
    expect(link(fixture, '/settings/arr/sonarr')).not.toBeNull();
  });

  it('hides the grouped items when a section is folded away', () => {
    const { fixture } = setup();
    const component = fixture.componentInstance;

    component.toggleMediaApps();
    fixture.detectChanges();

    expect(component.mediaAppsExpanded()).toBe(false);
    expect(link(fixture, '/settings/arr/sonarr')).toBeNull();
    expect(link(fixture, '/settings/general')).not.toBeNull();

    component.toggleSettings();
    fixture.detectChanges();

    expect(component.settingsExpanded()).toBe(false);
    expect(link(fixture, '/settings/general')).toBeNull();
    expect(link(fixture, '/dashboard')).not.toBeNull();
  });

  it('announces a nav click only on mobile and signs the user out on request', () => {
    const { fixture, logouts } = setup();
    let clicks = 0;
    fixture.componentInstance.navClicked.subscribe(() => clicks++);

    link(fixture, '/logs').click();
    fixture.detectChanges();
    expect(clicks).toBe(0);

    fixture.componentRef.setInput('isMobile', true);
    fixture.detectChanges();
    link(fixture, '/logs').click();
    fixture.detectChanges();
    expect(clicks).toBe(1);

    (fixture.nativeElement.querySelector('.sidebar__logout') as HTMLButtonElement).click();
    expect(logouts).toHaveLength(1);
  });
});
