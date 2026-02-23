import { Component, ChangeDetectionStrategy, input, output, signal, inject, computed } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { AppHubService } from '@core/realtime/app-hub.service';
import { AuthService } from '@core/auth/auth.service';

interface NavItem {
  label: string;
  icon?: string;
  iconSrc?: string;
  route: string;
}

interface ExternalLink {
  label: string;
  icon: string;
  href: string;
}

@Component({
  selector: 'app-nav-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgIcon],
  templateUrl: './nav-sidebar.component.html',
  styleUrl: './nav-sidebar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavSidebarComponent {
  private readonly hub = inject(AppHubService);
  private readonly auth = inject(AuthService);

  collapsed = input(false);
  mobileOpen = input(false);
  isMobile = input(false);
  navClicked = output<void>();

  settingsExpanded = signal(true);
  mediaAppsExpanded = signal(true);

  readonly currentVersion = computed(() => this.hub.appStatus()?.currentVersion ?? null);
  readonly latestVersion = computed(() => this.hub.appStatus()?.latestVersion ?? null);
  readonly hasUpdate = computed(() => {
    const current = this.currentVersion();
    const latest = this.latestVersion();
    return current && latest && current !== latest;
  });

  mainNavItems: NavItem[] = [
    { label: 'Dashboard', icon: 'tablerLayoutDashboard', route: '/dashboard' },
    { label: 'Logs', icon: 'tablerFileText', route: '/logs' },
    { label: 'Events', icon: 'tablerBell', route: '/events' },
    { label: 'Strikes', icon: 'tablerBolt', route: '/strikes' },
  ];

  settingsItems: NavItem[] = [
    { label: 'General', icon: 'tablerSettings', route: '/settings/general' },
    { label: 'Queue Cleaner', icon: 'tablerPlaylistX', route: '/settings/queue-cleaner' },
    { label: 'Malware Blocker', icon: 'tablerShieldLock', route: '/settings/malware-blocker' },
    { label: 'Download Cleaner', icon: 'tablerTrash', route: '/settings/download-cleaner' },
    { label: 'Blacklist Sync', icon: 'tablerBan', route: '/settings/blacklist-sync' },
  ];

  mediaAppItems: NavItem[] = [
    { label: 'Sonarr', iconSrc: 'icons/ext/sonarr-light.svg', route: '/settings/arr/sonarr' },
    { label: 'Radarr', iconSrc: 'icons/ext/radarr-light.svg', route: '/settings/arr/radarr' },
    { label: 'Lidarr', iconSrc: 'icons/ext/lidarr-light.svg', route: '/settings/arr/lidarr' },
    { label: 'Readarr', iconSrc: 'icons/ext/readarr-light.svg', route: '/settings/arr/readarr' },
    { label: 'Whisparr', iconSrc: 'icons/ext/whisparr-light.svg', route: '/settings/arr/whisparr' },
    { label: 'Download Clients', icon: 'tablerDownload', route: '/settings/download-clients' },
  ];

  otherSettingsItems: NavItem[] = [
    { label: 'Notifications', icon: 'tablerBellRinging', route: '/settings/notifications' },
    { label: 'Account', icon: 'tablerUser', route: '/settings/account' },
  ];

  suggestedApps: ExternalLink[] = [
  ];

  onNavItemClick(): void {
    if (this.isMobile()) {
      this.navClicked.emit();
    }
  }

  toggleSettings(): void {
    this.settingsExpanded.set(!this.settingsExpanded());
  }

  toggleMediaApps(): void {
    this.mediaAppsExpanded.set(!this.mediaAppsExpanded());
  }

  logout(): void {
    this.auth.logout();
  }
}
