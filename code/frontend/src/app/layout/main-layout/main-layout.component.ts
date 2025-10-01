import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

// PrimeNG Imports
import { ButtonModule } from 'primeng/button';
import { ToolbarModule } from 'primeng/toolbar';
import { FormsModule } from '@angular/forms';
import { MenuModule } from 'primeng/menu';
import { SidebarModule } from 'primeng/sidebar';

import { DividerModule } from 'primeng/divider';
import { RippleModule } from 'primeng/ripple';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

// Custom Components
import { SidebarContentComponent } from '../sidebar-content/sidebar-content.component';
import { ToastContainerComponent } from '../../shared/components/toast-container/toast-container.component';
import { AppHubService } from '../../core/services/app-hub.service';

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
    DividerModule,
    RippleModule,
    ConfirmDialogModule,
    SidebarContentComponent,
    ToastContainerComponent
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent implements OnInit {
  // Inject router
  public router = inject(Router);
  private readonly appHubService = inject(AppHubService);
  public readonly appStatus$ = this.appHubService.getAppStatus();
  
  ngOnInit(): void {
    this.appHubService.startConnection().catch((error: Error) => console.error('Failed to connect to app hub:', error));
  }
  
  /**
   * Toggle mobile sidebar visibility via sidebar component
   */
  toggleMobileSidebar(): void {
    // This will be called via template reference
  }
}
