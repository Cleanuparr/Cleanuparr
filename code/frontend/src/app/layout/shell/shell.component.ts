import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
  DestroyRef,
  HostListener,
  OnInit,
  OnDestroy,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
  private destroyRef = inject(DestroyRef);

  sidebarCollapsed = signal(false);
  mobileMenuOpen = signal(false);
  isMobile = signal(false);

  private readonly MOBILE_BREAKPOINT = 768;
  private readonly TABLET_BREAKPOINT = 1024;
  private autoCollapsed = signal(false);

  ngOnInit(): void {
    this.checkMobile();
    this.hub.start();

    // Auto-close mobile menu on navigation
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
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
      this.autoCollapsed.set(false);
    }
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  private checkMobile(): void {
    const width = window.innerWidth;
    const mobile = width <= this.MOBILE_BREAKPOINT;
    const tablet = !mobile && width <= this.TABLET_BREAKPOINT;

    this.isMobile.set(mobile);

    if (mobile) {
      return;
    }

    this.mobileMenuOpen.set(false);

    if (tablet) {
      if (!this.sidebarCollapsed()) {
        this.sidebarCollapsed.set(true);
        this.autoCollapsed.set(true);
      }
    } else if (this.autoCollapsed()) {
      this.sidebarCollapsed.set(false);
      this.autoCollapsed.set(false);
    }
  }
}
