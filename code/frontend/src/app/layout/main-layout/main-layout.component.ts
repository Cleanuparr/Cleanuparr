import { Component, inject, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Title } from '@angular/platform-browser';

// PrimeNG Imports
import { ButtonModule } from 'primeng/button';
import { ToolbarModule } from 'primeng/toolbar';
import { FormsModule } from '@angular/forms';
import { MenuModule } from 'primeng/menu';
import { SidebarModule } from 'primeng/sidebar';
import { DrawerModule } from 'primeng/drawer';
import { DividerModule } from 'primeng/divider';
import { RippleModule } from 'primeng/ripple';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

// Custom Components
import { SidebarContentComponent } from '../sidebar-content/sidebar-content.component';
import { ToastContainerComponent } from '../../shared/components/toast-container/toast-container.component';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    ButtonModule,
    ToolbarModule,
    FormsModule,
    MenuModule,
    SidebarModule,
    DrawerModule,
    DividerModule,
    RippleModule,
    ConfirmDialogModule,
    SidebarContentComponent,
    ToastContainerComponent
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent {
  // Mobile menu state
  mobileSidebarVisible = signal<boolean>(false);
  
  // Inject router
  public router = inject(Router);
  
  constructor() {}
  
  /**
   * Handles mobile navigation click events by closing the sidebar
   */
  onMobileNavClick(): void {
    this.mobileSidebarVisible.set(false);
  }
  
  /**
   * Toggle mobile sidebar visibility
   */
  toggleMobileSidebar(): void {
    this.mobileSidebarVisible.update(value => !value);
  }
}
