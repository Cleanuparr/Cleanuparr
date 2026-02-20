import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from '@core/services/theme.service';
import { AuthService } from '@core/auth/auth.service';
import { ToastContainerComponent, ConfirmDialogComponent } from '@ui';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastContainerComponent, ConfirmDialogComponent],
  template: `
    <router-outlet />
    <app-toast-container />
    <app-confirm-dialog />
  `,
})
export class App implements OnInit {
  // Inject ThemeService eagerly so it binds theme to DOM on startup
  private themeService = inject(ThemeService);
  private auth = inject(AuthService);

  ngOnInit(): void {
    this.auth.checkStatus().subscribe();
  }
}
