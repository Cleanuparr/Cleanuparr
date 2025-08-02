import { Component, Input, inject, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { trigger, state, style, transition, animate, query, stagger } from '@angular/animations';

interface MenuItem {
  label: string;
  icon: string;
  route: string;
  badge?: string;
}

interface NavigationItem {
  id: string;
  label: string;
  icon: string;
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
    ButtonModule
  ],
  templateUrl: './sidebar-content.component.html',
  styleUrl: './sidebar-content.component.scss',
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
export class SidebarContentComponent implements OnInit, OnChanges, OnDestroy {
  @Input() menuItems: MenuItem[] = [];
  @Input() isMobile = false;
  @Output() navItemClicked = new EventEmitter<void>();
  
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

  // Route synchronization properties
  private routerSubscription?: Subscription;
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
    { route: '/content-blocker', navigationPath: ['settings', 'content-blocker'] },
    { route: '/download-cleaner', navigationPath: ['settings', 'download-cleaner'] },
    { route: '/notifications', navigationPath: ['settings', 'notifications'] },
    
    // Other routes will be handled dynamically
  ];

  ngOnInit(): void {
    // Start with loading state
    this.isNavigationReady = false;
    
    // Initialize navigation after showing skeleton
    setTimeout(() => {
      this.initializeNavigation();
    }, 100);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['menuItems']) {
      this.updateActivityItems();
    }
  }

  ngOnDestroy(): void {
    this.routerSubscription?.unsubscribe();
  }

  /**
   * Initialize navigation and determine correct level based on route
   */
  private initializeNavigation(): void {
    if (this.hasInitialized) return;
    
    // 1. Initialize navigation data
    this.setupNavigationData();
    
    // 2. Update activity items if available
    if (this.menuItems && this.menuItems.length > 0) {
      this.updateActivityItems();
    }
    
    // 3. Determine correct navigation level based on current route
    this.syncSidebarWithCurrentRoute();
    
    // 4. Mark as ready and subscribe to route changes
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
          { id: 'sonarr', label: 'Sonarr', icon: 'pi pi-play-circle', route: '/sonarr' },
          { id: 'radarr', label: 'Radarr', icon: 'pi pi-play-circle', route: '/radarr' },
          { id: 'lidarr', label: 'Lidarr', icon: 'pi pi-bolt', route: '/lidarr' },
          { id: 'readarr', label: 'Readarr', icon: 'pi pi-book', route: '/readarr' },
          { id: 'whisparr', label: 'Whisparr', icon: 'pi pi-lock', route: '/whisparr' },
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
          { id: 'content-blocker', label: 'Malware Blocker', icon: 'pi pi-shield', route: '/content-blocker' },
          { id: 'download-cleaner', label: 'Download Cleaner', icon: 'pi pi-trash', route: '/download-cleaner' },
          { id: 'notifications', label: 'Notifications', icon: 'pi pi-bell', route: '/notifications' }
        ]
      },
      {
        id: 'activity',
        label: 'Activity',
        icon: 'pi pi-chart-line',
        children: [] // Will be populated dynamically from menuItems
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
   * Update activity items from menuItems input
   */
  private updateActivityItems(): void {
    const activityItem = this.navigationData.find(item => item.id === 'activity');
    if (activityItem && this.menuItems) {
      activityItem.children = this.menuItems
        .filter(item => !['Dashboard', 'Settings'].includes(item.label))
        .map(item => ({
          id: item.label.toLowerCase().replace(/\s+/g, '-'),
          label: item.label,
          icon: item.icon,
          route: item.route,
          badge: item.badge
        }));
      
      // Update route mappings for activity items
      this.updateActivityRouteMappings();
      
      // Update current navigation if we're showing the root level
      if (this.navigationBreadcrumb.length === 0) {
        this.currentNavigation = this.buildTopLevelNavigation();
      }

      // Re-sync with current route to handle activity routes
      this.syncSidebarWithCurrentRoute();
    }
  }

  /**
   * Update route mappings for activity items
   */
  private updateActivityRouteMappings(): void {
    // Remove old activity mappings
    this.routeMappings = this.routeMappings.filter(mapping => 
      !mapping.navigationPath[0] || !mapping.navigationPath[0].startsWith('activity')
    );
    
    // Add new activity mappings
    const activityItem = this.navigationData.find(item => item.id === 'activity');
    if (activityItem?.children) {
      activityItem.children.forEach(child => {
        if (child.route) {
          this.routeMappings.push({
            route: child.route,
            navigationPath: ['activity', child.id]
          });
        }
      });
    }
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
  }
}
