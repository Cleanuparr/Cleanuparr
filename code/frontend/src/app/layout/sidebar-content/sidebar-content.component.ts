import { Component, Input, inject, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

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

@Component({
  selector: 'app-sidebar-content',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ButtonModule
  ],
  templateUrl: './sidebar-content.component.html',
  styleUrl: './sidebar-content.component.scss'
})
export class SidebarContentComponent implements OnInit, OnChanges {
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

  ngOnInit(): void {
    this.initializeNavigation();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['menuItems']) {
      this.updateActivityItems();
    }
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
      
      // Update current navigation if we're showing the root level
      if (this.navigationBreadcrumb.length === 0) {
        this.currentNavigation = [...this.navigationData];
      }
    }
  }

  /**
   * Navigate to a sub-level
   */
  navigateToLevel(item: NavigationItem): void {
    if (item.children && item.children.length > 0) {
      this.navigationBreadcrumb.push(item);
      this.currentNavigation = [...item.children];
      this.updateNavigationState();
    }
  }

  /**
   * Go back to the previous level
   */
  goBack(): void {
    if (this.navigationBreadcrumb.length > 0) {
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
