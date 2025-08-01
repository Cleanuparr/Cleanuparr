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
    // Main navigation container animation
    trigger('slideInOut', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateX(-20px)' }),
        animate('300ms ease-out', style({ opacity: 1, transform: 'translateX(0)' }))
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0, transform: 'translateX(20px)' }))
      ])
    ]),
    
    // Individual navigation items animation
    trigger('staggerItems', [
      transition(':enter', [
        query('.nav-item', [
          style({ opacity: 0, transform: 'translateY(-10px)' }),
          stagger(50, animate('200ms ease-out', style({ opacity: 1, transform: 'translateY(0)' })))
        ], { optional: true })
      ])
    ]),
    
    // Breadcrumb animation
    trigger('breadcrumbSlide', [
      transition(':enter', [
        style({ opacity: 0, height: 0, marginBottom: 0 }),
        animate('250ms ease-out', style({ opacity: 1, height: '*', marginBottom: '10px' }))
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0, height: 0, marginBottom: 0 }))
      ])
    ]),
    
    // Go back button animation
    trigger('slideDown', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-20px)' }),
        animate('250ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ]),
      transition(':leave', [
        animate('200ms ease-in', style({ opacity: 0, transform: 'translateY(-20px)' }))
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
  isInitializing = true;

  // Route synchronization properties
  private routerSubscription?: Subscription;
  private routeMappings: RouteMapping[] = [
    // Dashboard
    { route: '/dashboard', navigationPath: ['dashboard'] },
    
    // Media Management routes
    { route: '/sonarr', navigationPath: ['media-management', 'sonarr'] },
    { route: '/radarr', navigationPath: ['media-management', 'radarr'] },
    { route: '/lidarr', navigationPath: ['media-management', 'lidarr'] },
    { route: '/readarr', navigationPath: ['media-management', 'readarr'] },
    { route: '/whisparr', navigationPath: ['media-management', 'whisparr'] },
    { route: '/download-clients', navigationPath: ['media-management', 'download-clients'] },
    
    // System routes
    { route: '/settings', navigationPath: ['system', 'cleanup'] },
    { route: '/notifications', navigationPath: ['system', 'notifications'] },
    
    // Activity routes will be handled dynamically
  ];

  ngOnInit(): void {
    this.initializeNavigationWithRoute();
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
   * Initialize navigation with correct route state from the start
   */
  private async initializeNavigationWithRoute(): Promise<void> {
    // Initialize navigation data first
    this.initializeNavigation();
    
    // Determine correct state before rendering
    await this.determineInitialNavigationState();
    
    // Mark as ready to render
    this.isNavigationReady = true;
    this.isInitializing = false;
    
    // Subscribe to route changes for future navigation
    this.subscribeToRouteChanges();
  }

  /**
   * Determine initial navigation state based on current route
   */
  private async determineInitialNavigationState(): Promise<void> {
    // Give Angular time to process route
    await new Promise(resolve => setTimeout(resolve, 0));
    
    const currentRoute = this.router.url;
    const mapping = this.findRouteMapping(currentRoute);
    
    if (mapping) {
      this.navigateToRouteMapping(mapping);
    }
  }

  /**
   * TrackBy function for better performance
   */
  trackByItemId(index: number, item: NavigationItem): string {
    return item.id;
  }

  /**
   * TrackBy function for breadcrumb items
   */
  trackByBreadcrumb(index: number, item: NavigationItem): string {
    return `${item.id}-${index}`;
  }

  /**
   * Initialize the navigation data structure
   */
  private initializeNavigation(): void {
    this.navigationData = [
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: 'pi pi-home',
        route: '/dashboard'
      },
      {
        id: 'media-management',
        label: 'Media Management',
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
        id: 'system',
        label: 'System',
        icon: 'pi pi-cog',
        children: [
          { id: 'cleanup', label: 'Cleanup', icon: 'pi pi-trash', route: '/settings' },
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
          {
            id: 'recommended-apps',
            label: 'Recommended Apps',
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
        ]
      }
    ];

    // Set initial navigation to root level
    this.currentNavigation = [...this.navigationData];
    this.updateActivityItems();
    this.updateNavigationState();
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
        this.currentNavigation = [...this.navigationData];
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
      !mapping.navigationPath[0]?.startsWith('activity')
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
   * Navigate sidebar to match route mapping
   */
  private navigateToRouteMapping(mapping: RouteMapping): void {
    // Reset to root level
    this.navigationBreadcrumb = [];
    this.currentNavigation = [...this.navigationData];
    
    // Navigate through the path to reach the target (skip the last item as it's the final destination)
    for (let i = 0; i < mapping.navigationPath.length - 1; i++) {
      const itemId = mapping.navigationPath[i];
      const item = this.currentNavigation.find(nav => nav.id === itemId);
      
      if (item && item.children) {
        this.navigationBreadcrumb.push(item);
        this.currentNavigation = [...item.children];
      }
    }
    
    this.updateNavigationState();
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
   * Navigate to a sub-level with smooth animation
   */
  navigateToLevel(item: NavigationItem): void {
    if (item.children && item.children.length > 0) {
      // Add small delay for smooth animation
      this.isNavigationReady = false;
      
      setTimeout(() => {
        this.navigationBreadcrumb.push(item);
        this.currentNavigation = item.children ? [...item.children] : [];
        this.updateNavigationState();
        this.isNavigationReady = true;
      }, 150);
    }
  }

  /**
   * Go back to the previous level with smooth animation
   */
  goBack(): void {
    if (this.navigationBreadcrumb.length > 0) {
      this.isNavigationReady = false;
      
      setTimeout(() => {
        this.navigationBreadcrumb.pop();
        
        if (this.navigationBreadcrumb.length === 0) {
          // Back to root level
          this.currentNavigation = [...this.navigationData];
        } else {
          // Back to parent level
          const parent = this.navigationBreadcrumb[this.navigationBreadcrumb.length - 1];
          this.currentNavigation = parent.children ? [...parent.children] : [];
        }
        
        this.updateNavigationState();
        this.isNavigationReady = true;
      }, 150);
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
