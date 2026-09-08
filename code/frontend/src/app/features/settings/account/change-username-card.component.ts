import { Component, ChangeDetectionStrategy, inject, input, signal, effect, untracked } from '@angular/core';
import { form, required, validate, FormField } from '@angular/forms/signals';
import { CardComponent, InputComponent, ButtonComponent, SpinnerComponent } from '@ui';
import { AccountApi } from '@core/api/account.api';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/services/toast.service';

interface ChangeUsernameFormModel {
  newUsername: string;
  currentPassword: string;
}

@Component({
  selector: 'app-change-username-card',
  standalone: true,
  imports: [CardComponent, InputComponent, ButtonComponent, SpinnerComponent, FormField],
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

  private readonly model = signal<ChangeUsernameFormModel>({
    newUsername: '',
    currentPassword: '',
  });

  readonly usernameForm = form(this.model, (p) => {
    required(p.newUsername, { message: 'Username is required' });
    validate(p.newUsername, () => {
      return this.model().newUsername.trim().length < 3
        ? { kind: 'minLength', message: 'Username must be at least 3 characters' }
        : undefined;
    });
    validate(p.newUsername, () => {
      return this.model().newUsername.trim() === this.currentUsername()
        ? { kind: 'unchanged', message: 'New username must be different from the current username' }
        : undefined;
    });

    required(p.currentPassword, { message: 'Current password is required' });
  });

  readonly changingUsername = signal(false);

  constructor() {
    effect(() => {
      const username = this.currentUsername();
      untracked(() => {
        this.model.set({ newUsername: username, currentPassword: '' });
      });
    });
  }

  changeUsername(): void {
    this.changingUsername.set(true);
    this.api.changeUsername({
      currentPassword: this.model().currentPassword,
      newUsername: this.model().newUsername.trim(),
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
