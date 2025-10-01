import { Component, Input, inject, Output, EventEmitter, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { filter, debounceTime } from 'rxjs/operators';
import { Subscription, fromEvent } from 'rxjs';
import { trigger, state, style, transition, animate, query, stagger } from '@angular/animations';

interface NavigationItem {
  id: string;
  label: string;
  icon: string;
  iconUrl?: string;
  iconUrlHover?: string;
  route?: string;           // For direct navigation items
  children?: NavigationItem[]; // For parent items with sub-menus
  isExternal?: boolean;     // For external links
  href?: string;           // For external URLs
  badge?: string;          // For notification badges
  topLevel?: boolean;      // If true, shows children directly on top level instead of drill-down
  isHeader?: boolean;      // If true, renders as a section header (non-clickable)
}

interface RouteMapping {
  route: string;
  navigationPath: string[]; // Array of navigation item IDs leading to this route
}

@Component({
  selector: 'app-sidebar-content',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ButtonModule,
    DrawerModule
  ],
  templateUrl: './sidebar-content.component.html',
  styleUrl: './sidebar-content.component.scss',
  host: {
    '[class.mobile-variant]': 'isMobile'
  },
  animations: [
    trigger('staggerItems', [
      transition(':enter', [
        query(':enter', [
          style({ transform: 'translateX(30px)', opacity: 0 }),
          stagger('50ms', [
            animate('300ms cubic-bezier(0.4, 0.0, 0.2, 1)', style({ transform: 'translateX(0)', opacity: 1 }))
          ])
        ], { optional: true })
      ])
    ]),
    // Container-level navigation animation (replaces individual item animations)
    trigger('navigationContainer', [
      transition('* => *', [
        style({ transform: 'translateX(100%)', opacity: 0 }),
        animate('300ms cubic-bezier(0.4, 0.0, 0.2, 1)', 
          style({ transform: 'translateX(0)', opacity: 1 })
        )
      ])
    ]),
    // Simple fade in animation for initial load
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('200ms ease-out', style({ opacity: 1 }))
      ])
    ])
  ]
})
export class SidebarContentComponent implements OnInit, OnDestroy {
  @Input() isMobile = false;
  @Input() enableMobileDrawer = false;
  @Output() navItemClicked = new EventEmitter<void>();
  @Output() mobileDrawerVisibilityChange = new EventEmitter<boolean>();
  
  // Mobile drawer state
  mobileSidebarVisible = signal<boolean>(false);
  
  // Inject router for active route styling
  public router = inject(Router);
  
  // New properties for drill-down navigation
  navigationData: NavigationItem[] = [];
  currentNavigation: NavigationItem[] = [];
  navigationBreadcrumb: NavigationItem[] = [];
  canGoBack = false;

  // Pre-rendering optimization properties
  isNavigationReady = false;
  private hasInitialized = false;

  // Animation trigger property - changes to force re-render and trigger animations
  navigationStateKey = 0;

  // Track hovered navigation item id to swap images
  hoveredNavId: string | null = null;

  // Route synchronization properties
  private routerSubscription?: Subscription;
  private resizeSubscription?: Subscription;
  private routeMappings: RouteMapping[] = [
    // Dashboard
    { route: '/dashboard', navigationPath: ['dashboard'] },
    
    // Media Management routes
    { route: '/sonarr', navigationPath: ['media-apps', 'sonarr'] },
    { route: '/radarr', navigationPath: ['media-apps', 'radarr'] },
    { route: '/lidarr', navigationPath: ['media-apps', 'lidarr'] },
    { route: '/readarr', navigationPath: ['media-apps', 'readarr'] },
    { route: '/whisparr', navigationPath: ['media-apps', 'whisparr'] },
    { route: '/download-clients', navigationPath: ['media-apps', 'download-clients'] },
    
    // Settings routes
    { route: '/general-settings', navigationPath: ['settings', 'general'] },
    { route: '/queue-cleaner', navigationPath: ['settings', 'queue-cleaner'] },
    { route: '/malware-blocker', navigationPath: ['settings', 'malware-blocker'] },
    { route: '/download-cleaner', navigationPath: ['settings', 'download-cleaner'] },
    { route: '/blacklist-synchronizer', navigationPath: ['settings', 'blacklist-synchronizer'] },
    { route: '/notifications', navigationPath: ['settings', 'notifications'] },
    
    // Activity routes
    { route: '/logs', navigationPath: ['activity', 'logs'] },
    { route: '/events', navigationPath: ['activity', 'events'] }
  ];

  ngOnInit(): void {
    // Start with loading state
    this.isNavigationReady = false;
    
    // Initialize navigation after showing skeleton
    setTimeout(() => {
      this.initializeNavigation();
    }, 100);

    // Listen for window resize events to auto-hide mobile drawer
    this.setupWindowResizeListener();
  }

  ngOnDestroy(): void {
    this.routerSubscription?.unsubscribe();
    this.resizeSubscription?.unsubscribe();
  }

  /**
   * Setup window resize listener to auto-hide mobile drawer on larger screens
   */
  private setupWindowResizeListener(): void {
    // Define the mobile breakpoint (should match CSS media query)
    const MOBILE_BREAKPOINT = 991;

    this.resizeSubscription = fromEvent(window, 'resize')
      .pipe(
        debounceTime(150), // Debounce resize events for better performance
        filter(() => window.innerWidth > MOBILE_BREAKPOINT && this.mobileSidebarVisible())
      )
      .subscribe(() => {
        this.mobileSidebarVisible.set(false);
      });
  }

  /**
   * Initialize navigation and determine correct level based on route
   */
  private initializeNavigation(): void {
    if (this.hasInitialized) return;
    
    this.setupNavigationData();
    
    this.syncSidebarWithCurrentRoute();
    
    this.isNavigationReady = true;
    this.hasInitialized = true;
    this.subscribeToRouteChanges();
  }

  /**
   * Setup basic navigation data structure
   */
  private setupNavigationData(): void {
    this.navigationData = this.getNavigationData();
    this.currentNavigation = this.buildTopLevelNavigation();
    // Preload hover icons to avoid flicker on first hover
    this.preloadIcons();
  }

  /**
   * Preload hover icon images to reduce flicker when user first hovers over an item
   */
  private preloadIcons(): void {
    const urls = new Set<string>();
    const collect = (items: NavigationItem[] | undefined) => {
      if (!items) return;
      items.forEach(i => {
        if (i.iconUrlHover) urls.add(i.iconUrlHover);
        if (i.children) collect(i.children);
      });
    };

    collect(this.navigationData);

    urls.forEach(url => {
      const img = new Image();
      img.src = url;
    });
  }

  /**
   * Build top-level navigation including expanded sections marked with topLevel: true
   */
  private buildTopLevelNavigation(): NavigationItem[] {
    const topLevelItems: NavigationItem[] = [];
    
    for (const item of this.navigationData) {
      if (item.topLevel && item.children) {
        // Add section header
        topLevelItems.push({
          id: `${item.id}-header`,
          label: item.label,
          icon: item.icon,
          isHeader: true
        });
        
        // Add all children directly to top level
        topLevelItems.push(...item.children);
      } else {
        // Add item normally (drill-down behavior)
        topLevelItems.push(item);
      }
    }
    
    return topLevelItems;
  }

  /**
   * Get the navigation data structure
   */
  private getNavigationData(): NavigationItem[] {
    return [
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: 'pi pi-home',
        route: '/dashboard'
      },
      {
        id: 'media-apps',
        label: 'Media Apps',
        icon: 'pi pi-play-circle',
        children: [
          {
            id: 'sonarr',
            label: 'Sonarr',
            icon: 'pi pi-play-circle',
            route: '/sonarr',
            iconUrl: 'icons/ext/sonarr-light.svg',
            iconUrlHover: 'icons/ext/sonarr.svg'
          },
          {
            id: 'radarr',
            label: 'Radarr',
            icon: 'pi pi-play-circle',
            route: '/radarr',
            iconUrl: 'icons/ext/radarr-light.svg',
            iconUrlHover: 'icons/ext/radarr.svg'
          },
          {
            id: 'lidarr',
            label: 'Lidarr',
            icon: 'pi pi-bolt',
            route: '/lidarr',
            iconUrl: 'icons/ext/lidarr-light.svg',
            iconUrlHover: 'icons/ext/lidarr.svg'
          },
          {
            id: 'readarr',
            label: 'Readarr',
            icon: 'pi pi-book',
            route: '/readarr',
            iconUrl: 'icons/ext/readarr-light.svg',
            iconUrlHover: 'icons/ext/readarr.svg'
          },
          {
            id: 'whisparr',
            label: 'Whisparr',
            icon: 'pi pi-lock',
            route: '/whisparr',
            iconUrl: 'icons/ext/whisparr-light.svg',
            iconUrlHover: 'icons/ext/whisparr.svg'
          },
          { id: 'download-clients', label: 'Download Clients', icon: 'pi pi-download', route: '/download-clients' }
        ]
      },
      {
        id: 'settings',
        label: 'Settings',
        icon: 'pi pi-cog',
        children: [
          { id: 'general', label: 'General', icon: 'pi pi-cog', route: '/general-settings' },
          { id: 'queue-cleaner', label: 'Queue Cleaner', icon: 'pi pi-list', route: '/queue-cleaner' },
          { id: 'malware-blocker', label: 'Malware Blocker', icon: 'pi pi-shield', route: '/malware-blocker' },
          { id: 'download-cleaner', label: 'Download Cleaner', icon: 'pi pi-trash', route: '/download-cleaner' },
          { id: 'blacklist-synchronizer', label: 'Blacklist Synchronizer', icon: 'pi pi-sync', route: '/blacklist-synchronizer' },
          { id: 'notifications', label: 'Notifications', icon: 'pi pi-bell', route: '/notifications' }
        ]
      },
      {
        id: 'activity',
        label: 'Activity',
        icon: 'pi pi-chart-line',
        children: [
          { id: 'logs', label: 'Logs', icon: 'pi pi-list', route: '/logs' },
          { id: 'events', label: 'Events', icon: 'pi pi-calendar', route: '/events' }
        ]
      },
      {
        id: 'help-support',
        label: 'Help & Support',
        icon: 'pi pi-question-circle',
        children: [
          { 
            id: 'issues', 
            label: 'Issues and Requests', 
            icon: 'pi pi-github', 
            isExternal: true, 
            href: 'https://github.com/Cleanuparr/Cleanuparr/issues' 
          },
          { 
            id: 'discord', 
            label: 'Discord', 
            icon: 'pi pi-discord', 
            isExternal: true, 
            href: 'https://discord.gg/SCtMCgtsc4' 
          },
        ]
      },
      {
        id: 'suggested-apps',
        label: 'Suggested Apps',
        topLevel: true,
        icon: 'pi pi-star',
        children: [
          { 
            id: 'huntarr', 
            label: 'Huntarr', 
            icon: 'pi pi-github', 
            isExternal: true, 
            href: 'https://github.com/plexguide/Huntarr.io' 
          }
        ]
      }
    ];
  }

  /**
   * Navigate to route mapping synchronously without delays
   */
  private navigateToRouteMappingSync(mapping: RouteMapping): void {
    // No delays, no async operations - just set the state
    this.navigationBreadcrumb = [];
    this.currentNavigation = this.buildTopLevelNavigation();
    
    for (let i = 0; i < mapping.navigationPath.length - 1; i++) {
      const itemId = mapping.navigationPath[i];
      // Find in original navigation data, not the flattened version
      const item = this.navigationData.find(nav => nav.id === itemId);
      
      if (item && item.children && !item.topLevel) {
        // Only drill down if it's not a top-level section
        this.navigationBreadcrumb.push(item);
        this.currentNavigation = [...item.children];
      }
    }
    
    this.updateNavigationState();
  }

  /**
   * Get skeleton items based on predicted navigation state
   */
  getSkeletonItems(): Array<{isSponsor: boolean}> {
    const currentRoute = this.router.url;
    const mapping = this.findRouteMapping(currentRoute);
    
    if (mapping && mapping.navigationPath.length > 1) {
      // We'll show sub-navigation, predict item count
      return [
        { isSponsor: true },
        { isSponsor: false }, // Go back
        ...Array(6).fill({ isSponsor: false }) // Estimated items
      ];
    }
    
    // Default main navigation count
    return [
      { isSponsor: true },
      ...Array(5).fill({ isSponsor: false })
    ];
  }

  /**
   * TrackBy function for better performance
   */
  trackByItemId(index: number, item: NavigationItem): string {
    return item.id;
  }

  /**
   * TrackBy function that includes navigation state for animation triggers
   */
  trackByItemIdWithState(index: number, item: NavigationItem): string {
    return `${item.id}-${this.navigationStateKey}`;
  }

  /**
   * TrackBy function for breadcrumb items
   */
  trackByBreadcrumb(index: number, item: NavigationItem): string {
    return `${item.id}-${index}`;
  }



  /**
   * Sync sidebar state with current route
   */
  private syncSidebarWithCurrentRoute(): void {
    const currentRoute = this.router.url;
    const mapping = this.findRouteMapping(currentRoute);
    
    if (mapping) {
      this.navigateToRouteMapping(mapping);
    }
  }

  /**
   * Find route mapping for current route
   */
  private findRouteMapping(route: string): RouteMapping | null {
    // Find exact match first, or routes that start with the mapping route
    const mapping = this.routeMappings.find(m => 
      route === m.route || route.startsWith(m.route + '/')
    );
    
    return mapping || null;
  }

  /**
   * Navigate sidebar to match route mapping (used by route sync)
   */
  private navigateToRouteMapping(mapping: RouteMapping): void {
    // Use the synchronous version
    this.navigateToRouteMappingSync(mapping);
  }

  /**
   * Subscribe to route changes for real-time synchronization
   */
  private subscribeToRouteChanges(): void {
    this.routerSubscription = this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.syncSidebarWithCurrentRoute();
      });
  }

  /**
   * Navigate to a sub-level with animation trigger
   */
  navigateToLevel(item: NavigationItem): void {
    if (item.children && item.children.length > 0) {
      this.navigationBreadcrumb.push(item);
      this.currentNavigation = item.children ? [...item.children] : [];
      this.navigationStateKey++; // Force animation trigger
      this.updateNavigationState();
    }
  }

  /**
   * Go back to the previous level with animation trigger
   */
  goBack(): void {
    if (this.navigationBreadcrumb.length > 0) {
      this.navigationBreadcrumb.pop();
      
      if (this.navigationBreadcrumb.length === 0) {
        // Back to root level - use top-level navigation
        this.currentNavigation = this.buildTopLevelNavigation();
      } else {
        // Back to parent level
        const parent = this.navigationBreadcrumb[this.navigationBreadcrumb.length - 1];
        this.currentNavigation = parent.children ? [...parent.children] : [];
      }
      
      this.navigationStateKey++; // Force animation trigger
      this.updateNavigationState();
    }
  }

  /**
   * Update navigation state
   */
  private updateNavigationState(): void {
    this.canGoBack = this.navigationBreadcrumb.length > 0;
  }
  
  /**
   * Handle navigation item click
   */
  onNavItemClick(): void {
    if (this.isMobile) {
      this.navItemClicked.emit();
    }
    // Close mobile drawer when nav item is clicked
    if (this.mobileSidebarVisible()) {
      this.mobileSidebarVisible.set(false);
    }
  }

  /**
   * Show mobile drawer
   */
  showMobileDrawer(): void {
    this.mobileSidebarVisible.set(true);
  }

  /**
   * Hide mobile drawer  
   */
  hideMobileDrawer(): void {
    this.mobileSidebarVisible.set(false);
  }

  /**
   * Toggle mobile drawer visibility
   */
  toggleMobileDrawer(): void {
    this.mobileSidebarVisible.update(visible => !visible);
  }

  /**
   * Handle mobile drawer visibility change
   */
  onMobileDrawerVisibilityChange(visible: boolean): void {
    this.mobileSidebarVisible.set(visible);
    this.mobileDrawerVisibilityChange.emit(visible);
  }
}
