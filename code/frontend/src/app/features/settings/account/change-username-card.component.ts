import { Component, ChangeDetectionStrategy, inject, input, signal, linkedSignal } from '@angular/core';
import { CardComponent, InputComponent, ButtonComponent, SpinnerComponent } from '@ui';
import { AccountApi } from '@core/api/account.api';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-change-username-card',
  standalone: true,
  imports: [CardComponent, InputComponent, ButtonComponent, SpinnerComponent],
  templateUrl: './change-username-card.component.html',
  styleUrl: './change-username-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeUsernameCardComponent {
  private readonly api = inject(AccountApi);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly currentUsername = input('');
  readonly oidcExclusiveMode = input(false);

  readonly newUsername = linkedSignal(() => this.currentUsername());
  readonly currentPassword = signal('');
  readonly changingUsername = signal(false);

  changeUsername(): void {
    const newUsername = this.newUsername().trim();

    if (newUsername.length < 3) {
      this.toast.error('Username must be at least 3 characters');
      return;
    }
    if (newUsername === this.currentUsername()) {
      this.toast.error('New username must be different from the current username');
      return;
    }

    this.changingUsername.set(true);
    this.api.changeUsername({
      currentPassword: this.currentPassword(),
      newUsername,
    }).subscribe({
      next: () => {
        this.toast.success('Username changed, please sign in again');
        this.changingUsername.set(false);
        this.auth.logout();
      },
      error: () => {
        this.toast.error('Failed to change username');
        this.changingUsername.set(false);
      },
    });
  }
}
