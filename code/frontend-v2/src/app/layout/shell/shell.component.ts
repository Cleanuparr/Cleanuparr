import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
  HostListener,
  OnInit,
  OnDestroy,
} from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { NavSidebarComponent } from '../nav-sidebar/nav-sidebar.component';
import { ToolbarComponent } from '../toolbar/toolbar.component';
import { AppHubService } from '@core/realtime/app-hub.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, NavSidebarComponent, ToolbarComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private hub = inject(AppHubService);

  sidebarCollapsed = signal(false);
  mobileMenuOpen = signal(false);
  isMobile = signal(false);

  private readonly MOBILE_BREAKPOINT = 768;

  ngOnInit(): void {
    this.checkMobile();
    this.hub.start();

    // Auto-close mobile menu on navigation
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => this.mobileMenuOpen.set(false));
  }

  ngOnDestroy(): void {
    this.hub.stop();
  }

  @HostListener('window:resize')
  onResize(): void {
    this.checkMobile();
  }

  toggleSidebar(): void {
    if (this.isMobile()) {
      this.mobileMenuOpen.set(!this.mobileMenuOpen());
    } else {
      this.sidebarCollapsed.set(!this.sidebarCollapsed());
    }
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  private checkMobile(): void {
    const mobile = window.innerWidth <= this.MOBILE_BREAKPOINT;
    this.isMobile.set(mobile);
    if (!mobile) {
      this.mobileMenuOpen.set(false);
    }
  }
}
